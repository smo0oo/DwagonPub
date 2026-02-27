using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class EnemyAbilityHolder : MonoBehaviour
{
    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;

    public static event Action<EnemyAbilityHolder, Ability, Vector3> OnMajorThreatTelegraphed;

    [Header("Component References")]
    public Transform projectileSpawnPoint;

    [Header("VFX Anchors")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public Transform headAnchor;
    public Transform feetAnchor;
    public Transform centerAnchor;

    [Header("Visual Feedback (AAA)")]
    public GameObject dangerZonePrefab;

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
    private GameObject activeTelegraphInstance;
    private Collider[] hitBuffer = new Collider[20];

    private Vector3 debugBoxCenter;
    private Vector3 debugBoxSize;
    private Quaternion debugBoxRotation;
    private float debugDisplayTime;

    private int attackTriggerHash;
    private int attackIndexHash;

    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;

    void Awake()
    {
        movementHandler = GetComponentInParent<IMovementHandler>();
        enemyAI = GetComponentInParent<EnemyAI>();
        animator = GetComponentInParent<Animator>();
        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;

        attackTriggerHash = Animator.StringToHash("AttackTrigger");
        attackIndexHash = Animator.StringToHash("AttackIndex");
    }

    void OnDisable() => CancelCast();

    public void CancelCast()
    {
        if (activeCastCoroutine != null) { StopCoroutine(activeCastCoroutine); activeCastCoroutine = null; }
        IsCasting = false;
        CleanupCastingVFX();
        CleanupTelegraph();
        if (ActiveBeam != null) { ActiveBeam.Interrupt(); ActiveBeam = null; }

        OnCastFinished?.Invoke();
    }

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown = false)
    {
        if (!CanUseAbility(ability, target)) return;
        Vector3 position = (target != null) ? target.transform.position : transform.position;

        // --- AAA RANDOMIZATION LOGIC (Calculated at the exact moment the cast starts) ---
        int styleIndex = ability.attackStyleIndex;
        if (ability.randomizeAttackStyle && ability.maxRandomVariants > 0)
        {
            styleIndex = UnityEngine.Random.Range(0, ability.maxRandomVariants);
        }
        else if (ability.attackStyleIndex <= 0 && string.IsNullOrEmpty(ability.overrideTriggerName))
        {
            styleIndex = UnityEngine.Random.Range(0, 3); // Fallback for basic enemies
        }
        // -------------------------------------------------------------------------------

        if (ability.castTime > 0 || ability.telegraphDuration > 0)
        {
            if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = StartCoroutine(PerformCast(ability, target, position, bypassCooldown, styleIndex));
        }
        else
        {
            ExecuteAbility(ability, target, position, bypassCooldown, true, styleIndex);
        }
    }

    private IEnumerator PerformCast(Ability ability, GameObject target, Vector3 position, bool bypassCooldown, int styleIndex)
    {
        IsCasting = true;
        try
        {
            if ((ability.telegraphDuration > 0 || ability.castTime > 0) && animator != null)
            {
                // Inject the rolled style index immediately so the wind-up animation can read it
                animator.SetInteger(attackIndexHash, styleIndex);

                if (!string.IsNullOrEmpty(ability.telegraphAnimationTrigger))
                    animator.SetTrigger(ability.telegraphAnimationTrigger);
            }

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

            GameObject telegraphToSpawn = ability.enemyTelegraphPrefab != null ? ability.enemyTelegraphPrefab : dangerZonePrefab;

            if (telegraphToSpawn != null)
            {
                Vector3 spawnPos = position;
                Quaternion spawnRot = Quaternion.identity;

                if (ability.abilityType == AbilityType.DirectionalMelee || ability.abilityType == AbilityType.Charge)
                {
                    spawnPos = transform.position;
                    Vector3 dir = (position - transform.position).normalized;
                    if (dir != Vector3.zero) spawnRot = Quaternion.LookRotation(dir);
                    else spawnRot = transform.rotation;
                }

                spawnPos += Vector3.up * 0.05f;
                activeTelegraphInstance = Instantiate(telegraphToSpawn, spawnPos, spawnRot);

                if (ability.abilityType == AbilityType.GroundAOE || ability.abilityType == AbilityType.GroundPlacement)
                {
                    float diameter = ability.aoeRadius > 0 ? ability.aoeRadius * 2f : 2f;
                    activeTelegraphInstance.transform.localScale = new Vector3(diameter, 1f, diameter);
                }
            }

            if (ability.isMajorTacticalThreat)
            {
                OnMajorThreatTelegraphed?.Invoke(this, ability, position);
            }

            if (ability.telegraphDuration > 0) yield return new WaitForSeconds(ability.telegraphDuration);

            OnCastStarted?.Invoke(ability.abilityName, ability.castTime);
            if (ability.castTime > 0) yield return new WaitForSeconds(ability.castTime);

            if (enemyAI != null && (enemyAI.Health.currentHealth <= 0 || !enemyAI.enabled)) yield break;

            // Pass the generated style index down to the execution phase
            ExecuteAbility(ability, target, position, bypassCooldown, true, styleIndex);
        }
        finally
        {
            CleanupCastingVFX();
            CleanupTelegraph();

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

    private void CleanupTelegraph()
    {
        if (activeTelegraphInstance != null) { Destroy(activeTelegraphInstance); activeTelegraphInstance = null; }
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false, bool triggerAnimation = true, int styleIndex = 0)
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

        if (ability.screenShakeIntensity > 0)
        {
            PlayerAbilityHolder.TriggerCameraShake(ability.screenShakeIntensity, ability.screenShakeDuration, transform.position);
        }

        if (triggerAnimation) TriggerAttackAnimation(ability, styleIndex);

        switch (ability.abilityType)
        {
            case AbilityType.TargetedMelee:
            case AbilityType.DirectionalMelee:
                if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
                activeCastCoroutine = StartCoroutine(PerformSmartMeleeAttack(ability));
                break;
            case AbilityType.TargetedProjectile:
            case AbilityType.ForwardProjectile: HandleProjectile(ability, target, position); break;
            case AbilityType.GroundAOE: HandleGroundAOE(ability, position); break;
            case AbilityType.GroundPlacement: HandleGroundPlacement(ability, position); break;
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

    private void TriggerAttackAnimation(Ability ability, int styleIndex)
    {
        if (animator != null)
        {
            animator.ResetTrigger(attackTriggerHash);

            if (!string.IsNullOrEmpty(ability.overrideTriggerName))
            {
                animator.SetTrigger(ability.overrideTriggerName);
            }
            else
            {
                animator.SetInteger(attackIndexHash, styleIndex);
                animator.SetTrigger(attackTriggerHash);
            }
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
        GameObject myCasterObj = myRoot != null ? myRoot.gameObject : (enemyAI != null ? enemyAI.gameObject : this.gameObject);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit.gameObject.layer == 21) continue;

            CharacterRoot targetRoot = hit.GetComponentInParent<CharacterRoot>();
            GameObject uniqueTargetObj = (targetRoot != null) ? targetRoot.gameObject : hit.gameObject;

            if (myCasterObj == uniqueTargetObj) continue;
            if (hitTargets.Contains(uniqueTargetObj)) continue;
            hitTargets.Add(uniqueTargetObj);

            Health targetHealth = uniqueTargetObj.GetComponentInChildren<Health>();
            if (targetHealth != null)
            {
                if (myCasterObj.layer != uniqueTargetObj.layer)
                {
                    foreach (var effect in ability.hostileEffects) effect.Apply(myCasterObj, uniqueTargetObj);
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
        HashSet<GameObject> affectedObjects = new HashSet<GameObject>();
        CharacterRoot casterRoot = this.GetComponentInParent<CharacterRoot>();
        GameObject myCasterObj = casterRoot != null ? casterRoot.gameObject : (enemyAI != null ? enemyAI.gameObject : this.gameObject);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit.gameObject.layer == 21) continue;
            if (Vector3.Distance(position, hit.transform.position) > ability.aoeRadius + 1.0f) continue;

            CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();
            GameObject hitObj = hitCharacter != null ? hitCharacter.gameObject : hit.gameObject;

            if (myCasterObj == hitObj || affectedObjects.Contains(hitObj)) continue;
            affectedObjects.Add(hitObj);

            if (myCasterObj.layer != hitObj.layer)
            {
                foreach (var effect in ability.hostileEffects) effect.Apply(myCasterObj, hitObj);
            }
            else
            {
                foreach (var effect in ability.friendlyEffects) effect.Apply(myCasterObj, hitObj);
            }
        }
    }

    private void HandleGroundPlacement(Ability ability, Vector3 position)
    {
        if (ability.placementPrefab != null)
        {
            GameObject placedObject = Instantiate(ability.placementPrefab, position, Quaternion.identity);

            CharacterRoot casterRoot = GetComponentInParent<CharacterRoot>();
            GameObject myCasterObj = casterRoot != null ? casterRoot.gameObject : (enemyAI != null ? enemyAI.gameObject : this.gameObject);

            if (placedObject.TryGetComponent<AreaBombardmentController>(out var bombardment))
            {
                bombardment.Initialize(myCasterObj, ability);
            }
            else if (placedObject.TryGetComponent<PlaceableTrap>(out var trap))
            {
                trap.owner = myCasterObj;
            }
        }
    }

    private void PayCostAndStartCooldown(Ability ability, bool bypassCooldown) { if (!bypassCooldown) { cooldowns[ability] = Time.time + ability.cooldown; if (ability.triggersGlobalCooldown) globalCooldownTimer = Time.time + globalCooldownDuration; } }
    public bool CanUseAbility(Ability ability, GameObject target) { if (ability == null || IsCasting) return false; if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false; if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false; return true; }
    private void HandleChanneledBeam(Ability ability, GameObject target) { GameObject prefab = ability.enemyProjectilePrefab ?? ability.playerProjectilePrefab; if (prefab != null) { GameObject beam = Instantiate(prefab, transform.position, transform.rotation); if (beam.TryGetComponent<ChanneledBeamController>(out var b)) { b.Initialize(ability, gameObject, target); ActiveBeam = b; } } }
    private void HandleSelfCast(Ability ability) { foreach (var effect in ability.friendlyEffects) effect.Apply(gameObject, gameObject); }
    private void HandleProjectile(Ability ability, GameObject target, Vector3 fallbackPosition)
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
        else
        {
            Vector3 dir = fallbackPosition - spawnPos; dir.y = 0;
            if (dir.sqrMagnitude > 0.001f) spawnRot = Quaternion.LookRotation(dir);
        }

        GameObject pGO = ObjectPooler.instance.Get(prefab, spawnPos, spawnRot);
        if (pGO == null) return;
        pGO.layer = LayerMask.NameToLayer("HostileRanged");
        Collider pCol = pGO.GetComponent<Collider>();
        if (pCol != null) foreach (Collider c in GetComponentsInParent<Collider>()) Physics.IgnoreCollision(pCol, c);

        if (pGO.TryGetComponent<Projectile>(out var p))
        {
            CharacterRoot myRoot = GetComponentInParent<CharacterRoot>();
            int layer = myRoot != null ? myRoot.gameObject.layer : gameObject.layer;
            p.Initialize(ability, gameObject, layer);
        }
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