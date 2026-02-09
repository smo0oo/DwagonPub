using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class EnemyAbilityHolder : MonoBehaviour
{
    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;

    [Header("Component References")]
    public Transform projectileSpawnPoint;

    [Header("VFX Anchors")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public Transform headAnchor;
    public Transform feetAnchor;
    public Transform centerAnchor;

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.5f;

    [Header("Targeting Config")]
    public LayerMask aoeTargetLayers = 1 << 0;

    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;

    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private IMovementHandler movementHandler;
    private Coroutine activeCastCoroutine;
    private EnemyAI enemyAI;
    private float globalCooldownTimer = 0f;
    private Animator animator;
    private GameObject currentCastingVFXInstance;
    private Collider[] hitBuffer = new Collider[20];

    // Debug Gizmos
    private Vector3 debugBoxCenter;
    private Vector3 debugBoxSize;
    private Quaternion debugBoxRotation;
    private float debugDisplayTime;

    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;

    void Awake()
    {
        movementHandler = GetComponentInParent<IMovementHandler>();
        enemyAI = GetComponentInParent<EnemyAI>();
        animator = GetComponentInParent<Animator>();
        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
    }

    void OnDisable() => CancelCast();

    public void CancelCast()
    {
        if (activeCastCoroutine != null) { StopCoroutine(activeCastCoroutine); activeCastCoroutine = null; }
        IsCasting = false;
        CleanupCastingVFX();
        if (ActiveBeam != null) { ActiveBeam.Interrupt(); ActiveBeam = null; }
    }

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown = false)
    {
        if (!CanUseAbility(ability, target)) return;
        Vector3 position = (target != null) ? target.transform.position : transform.position;

        if (ability.castTime > 0 || ability.telegraphDuration > 0)
        {
            if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = StartCoroutine(PerformCast(ability, target, position, bypassCooldown));
        }
        else ExecuteAbility(ability, target, position, bypassCooldown);
    }

    private IEnumerator PerformCast(Ability ability, GameObject target, Vector3 position, bool bypassCooldown)
    {
        IsCasting = true;
        try
        {
            if (ability.telegraphDuration > 0 && animator != null && !string.IsNullOrEmpty(ability.telegraphAnimationTrigger))
                animator.SetTrigger(ability.telegraphAnimationTrigger);

            if (ability.castingVFX != null)
            {
                Transform anchor = GetAnchorTransform(ability.castingVFXAnchor);
                currentCastingVFXInstance = ObjectPooler.instance.Get(ability.castingVFX, anchor.position, anchor.rotation);
                if (currentCastingVFXInstance != null)
                {
                    currentCastingVFXInstance.transform.SetParent(anchor);
                    currentCastingVFXInstance.transform.localPosition = ability.castingVFXPositionOffset;
                    currentCastingVFXInstance.transform.localRotation = Quaternion.Euler(ability.castingVFXRotationOffset);
                    currentCastingVFXInstance.SetActive(true);
                }
            }

            if (ability.telegraphDuration > 0) yield return new WaitForSeconds(ability.telegraphDuration);

            OnCastStarted?.Invoke(ability.abilityName, ability.castTime);
            if (ability.castTime > 0) yield return new WaitForSeconds(ability.castTime);

            if (enemyAI != null && (enemyAI.Health.currentHealth <= 0 || !enemyAI.enabled)) yield break;

            ExecuteAbility(ability, target, position, bypassCooldown);
        }
        finally
        {
            CleanupCastingVFX();
            if (ability.abilityType != AbilityType.TargetedMelee && ability.abilityType != AbilityType.DirectionalMelee)
            {
                IsCasting = false;
                activeCastCoroutine = null;
                OnCastFinished?.Invoke();
            }
        }
    }

    private void CleanupCastingVFX()
    {
        if (currentCastingVFXInstance != null)
        {
            PooledObject pooled = currentCastingVFXInstance.GetComponent<PooledObject>();
            if (pooled != null) pooled.ReturnToPool();
            else Destroy(currentCastingVFXInstance);
            currentCastingVFXInstance = null;
        }
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false)
    {
        if (enemyAI != null && enemyAI.Health.currentHealth <= 0) return;

        PayCostAndStartCooldown(ability, bypassCooldown);
        if (ability.castSound != null) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);

        if (ability.castVFX != null)
        {
            Transform anchor = GetAnchorTransform(ability.castVFXAnchor);
            GameObject vfx = ObjectPooler.instance.Get(ability.castVFX, anchor.position, anchor.rotation);
            if (vfx != null)
            {
                vfx.transform.localPosition += ability.castVFXPositionOffset;
                vfx.transform.localRotation *= Quaternion.Euler(ability.castVFXRotationOffset);
                vfx.SetActive(true);
            }
        }

        switch (ability.abilityType)
        {
            case AbilityType.TargetedMelee:
            case AbilityType.DirectionalMelee:
                if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
                activeCastCoroutine = StartCoroutine(PerformSmartMeleeAttack(ability));
                break;
            case AbilityType.TargetedProjectile:
            case AbilityType.ForwardProjectile: HandleProjectile(ability, target); break;
            case AbilityType.GroundAOE: HandleGroundAOE(ability, position); break;
            case AbilityType.Self: HandleSelfCast(ability); break;
            case AbilityType.ChanneledBeam: HandleChanneledBeam(ability, target); break;
            case AbilityType.Leap: if (movementHandler != null) movementHandler.ExecuteLeap(position, () => HandleGroundAOE(ability, position)); break;
            case AbilityType.Charge: if (enemyAI != null && target != null) enemyAI.ExecuteCharge(target, ability); break;
            case AbilityType.Teleport:
                if (movementHandler != null && enemyAI != null && enemyAI.currentTarget != null)
                {
                    Vector3 directionAway = (transform.position - enemyAI.currentTarget.position).normalized;
                    Vector3 destination = transform.position + directionAway * 15f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(destination, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                        movementHandler.ExecuteTeleport(hit.position);
                }
                break;
        }
    }

    private IEnumerator PerformSmartMeleeAttack(Ability ability)
    {
        IsCasting = true;
        float windup = (ability.hitboxOpenDelay > 0) ? ability.hitboxOpenDelay : 0.1f;
        yield return new WaitForSeconds(windup);

        if (enemyAI != null && enemyAI.Health.currentHealth <= 0) yield break;

        CheckHit(ability);

        float remainingDuration = ability.hitboxCloseDelay - ability.hitboxOpenDelay;
        if (remainingDuration <= 0.05f) remainingDuration = 0.25f;
        yield return new WaitForSeconds(remainingDuration);

        IsCasting = false;
        activeCastCoroutine = null;
        OnCastFinished?.Invoke();
    }

    private void CheckHit(Ability ability)
    {
        float boxLength = ability.range + 0.5f;
        float boxWidth = ability.attackBoxSize.x > 0 ? ability.attackBoxSize.x : 2f;
        float boxHeight = ability.attackBoxSize.y > 0 ? ability.attackBoxSize.y : 2f;

        Vector3 center = transform.position + (transform.forward * (boxLength / 2f)) + (Vector3.up * 1f);
        Vector3 halfExtents = new Vector3(boxWidth / 2f, boxHeight / 2f, boxLength / 2f);

        debugBoxCenter = center;
        debugBoxSize = halfExtents * 2;
        debugBoxRotation = transform.rotation;
        debugDisplayTime = 0.5f;

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, hitBuffer, transform.rotation, aoeTargetLayers);
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();
        CharacterRoot myRoot = GetComponentInParent<CharacterRoot>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];

            // 1. Ignore Activation Triggers
            if (hit.gameObject.layer == 21) continue;

            // 2. Identify Target Root
            CharacterRoot targetRoot = hit.GetComponentInParent<CharacterRoot>();

            // 3. [FIX] Compare CharacterRoots, not Transform.Root
            // Stop self-hit
            if (myRoot != null && targetRoot != null && myRoot == targetRoot) continue;

            // 4. Resolve Target Object
            GameObject uniqueTargetObj = (targetRoot != null) ? targetRoot.gameObject : hit.gameObject;

            if (hitTargets.Contains(uniqueTargetObj)) continue;
            hitTargets.Add(uniqueTargetObj);

            Health targetHealth = uniqueTargetObj.GetComponentInChildren<Health>();
            if (targetHealth != null)
            {
                if (myRoot != null && myRoot.gameObject.layer != uniqueTargetObj.layer)
                {
                    foreach (var effect in ability.hostileEffects) effect.Apply(myRoot.gameObject, uniqueTargetObj);
                }
            }
        }
    }

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null)
        {
            GameObject vfx = ObjectPooler.instance.Get(ability.hitVFX, position, Quaternion.identity);
            if (vfx != null) vfx.SetActive(true);
        }
        int hitCount = Physics.OverlapSphereNonAlloc(position, ability.aoeRadius, hitBuffer, aoeTargetLayers);
        List<CharacterRoot> affectedCharacters = new List<CharacterRoot>();
        CharacterRoot casterRoot = this.GetComponentInParent<CharacterRoot>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];

            // 1. Ignore Activation Triggers
            if (hit.gameObject.layer == 21) continue;

            // 2. Distance Check
            if (Vector3.Distance(position, hit.transform.position) > ability.aoeRadius + 1.0f) continue;

            // 3. Identify Target Root
            CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();

            // 4. [FIX] Avoid hitting self or duplicates
            if (hitCharacter == null || affectedCharacters.Contains(hitCharacter)) continue;
            if (casterRoot != null && hitCharacter == casterRoot) continue;

            affectedCharacters.Add(hitCharacter);

            if (casterRoot == null) return;
            if (casterRoot.gameObject.layer != hitCharacter.gameObject.layer)
            {
                foreach (var effect in ability.hostileEffects) effect.Apply(casterRoot.gameObject, hitCharacter.gameObject);
            }
            else
            {
                foreach (var effect in ability.friendlyEffects) effect.Apply(casterRoot.gameObject, hitCharacter.gameObject);
            }
        }
    }

    // [Standard Helpers unchanged]
    private void PayCostAndStartCooldown(Ability ability, bool bypassCooldown) { if (!bypassCooldown) { cooldowns[ability] = Time.time + ability.cooldown; if (ability.triggersGlobalCooldown) globalCooldownTimer = Time.time + globalCooldownDuration; } }
    public bool CanUseAbility(Ability ability, GameObject target) { if (ability == null || IsCasting) return false; if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false; if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false; return true; }
    private void HandleChanneledBeam(Ability ability, GameObject target) { GameObject prefab = ability.enemyProjectilePrefab ?? ability.playerProjectilePrefab; if (prefab != null) { GameObject beam = Instantiate(prefab, transform.position, transform.rotation); if (beam.TryGetComponent<ChanneledBeamController>(out var b)) { b.Initialize(ability, gameObject, target); ActiveBeam = b; } } }
    private void HandleSelfCast(Ability ability) { foreach (var effect in ability.friendlyEffects) effect.Apply(gameObject, gameObject); }
    private void HandleProjectile(Ability ability, GameObject target)
    {
        GameObject prefab = ability.enemyProjectilePrefab ?? ability.playerProjectilePrefab;
        if (prefab == null) return;
        Transform spawnT = projectileSpawnPoint ?? transform;
        Vector3 spawnPos = spawnT.position;
        if (spawnT == transform) spawnPos += Vector3.up * 1.5f;
        Quaternion spawnRot = spawnT.rotation;
        if (target != null)
        {
            Vector3 tPos = target.transform.position;
            if (target.GetComponent<Collider>() != null) tPos = target.GetComponent<Collider>().bounds.center;
            Vector3 dir = tPos - spawnPos; dir.y = 0;
            if (dir.sqrMagnitude > 0.001f) spawnRot = Quaternion.LookRotation(dir);
        }
        GameObject pGO = ObjectPooler.instance.Get(prefab, spawnPos, spawnRot);
        if (pGO == null) return;
        pGO.layer = LayerMask.NameToLayer("HostileRanged");
        Collider pCol = pGO.GetComponent<Collider>();
        if (pCol != null) foreach (Collider c in GetComponentInParent<CharacterRoot>().GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(pCol, c);
        if (pGO.TryGetComponent<Projectile>(out var p)) p.Initialize(ability, gameObject, GetComponentInParent<CharacterRoot>().gameObject.layer);
        pGO.SetActive(true);
    }
    private Transform GetAnchorTransform(VFXAnchor anchor)
    {
        if (anchor == VFXAnchor.LeftHand && leftHandAnchor) return leftHandAnchor;
        if (anchor == VFXAnchor.RightHand && rightHandAnchor) return rightHandAnchor;
        if (anchor == VFXAnchor.Head && headAnchor) return headAnchor;
        if (anchor == VFXAnchor.Feet && feetAnchor) return feetAnchor;
        if (anchor == VFXAnchor.Center && centerAnchor) return centerAnchor;
        if (animator && animator.isHuman)
        {
            if (anchor == VFXAnchor.LeftHand) return animator.GetBoneTransform(HumanBodyBones.LeftHand) ?? transform;
            if (anchor == VFXAnchor.RightHand) return animator.GetBoneTransform(HumanBodyBones.RightHand) ?? transform;
            if (anchor == VFXAnchor.Head) return animator.GetBoneTransform(HumanBodyBones.Head) ?? transform;
            if (anchor == VFXAnchor.Center) return animator.GetBoneTransform(HumanBodyBones.Chest) ?? transform;
        }
        return projectileSpawnPoint ?? transform;
    }
    void Update() { if (ActiveBeam != null && ActiveBeam.gameObject == null) ActiveBeam = null; }
    private void OnDrawGizmos() { if (debugDisplayTime > 0) { Gizmos.color = new Color(1, 0, 0, 0.3f); Gizmos.matrix = Matrix4x4.TRS(debugBoxCenter, debugBoxRotation, debugBoxSize); Gizmos.DrawCube(Vector3.zero, Vector3.one); debugDisplayTime -= Time.deltaTime; } }
}