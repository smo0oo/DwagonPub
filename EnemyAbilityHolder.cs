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

    // --- FIX: Added Missing Anchors to resolve CS0103 ---
    [Header("VFX Anchors (Optional)")]
    [Tooltip("Manually assign these for non-humanoid enemies or specific spawn points.")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public Transform headAnchor;
    public Transform feetAnchor;
    public Transform centerAnchor;
    // ----------------------------------------------------

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.5f;

    [Header("Targeting Config")]
    [Tooltip("Select 'Player' and 'Friendly' here. Uncheck 'Default' or 'Triggers' to stop hitting invisible zones.")]
    public LayerMask aoeTargetLayers = 1 << 0;

    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;

    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private IMovementHandler movementHandler;
    private Coroutine activeCastCoroutine;
    private EnemyAI enemyAI;
    private float globalCooldownTimer = 0f;
    private Animator animator;

    // VFX State (Tracked for cleanup)
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

    void OnDisable()
    {
        CancelCast();
    }

    public void CancelCast()
    {
        if (activeCastCoroutine != null)
        {
            StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = null;
        }
        IsCasting = false;
        CleanupCastingVFX();
        if (ActiveBeam != null)
        {
            ActiveBeam.Interrupt();
            ActiveBeam = null;
        }
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
        else
        {
            ExecuteAbility(ability, target, position, bypassCooldown);
        }
    }

    private IEnumerator PerformCast(Ability ability, GameObject target, Vector3 position, bool bypassCooldown)
    {
        IsCasting = true;
        try
        {
            // 1. Windup Animation
            if (ability.telegraphDuration > 0 && animator != null && !string.IsNullOrEmpty(ability.telegraphAnimationTrigger))
            {
                animator.SetTrigger(ability.telegraphAnimationTrigger);
            }

            // 2. Spawn Windup VFX (Visuals)
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

            // 3. Delays
            if (ability.telegraphDuration > 0) yield return new WaitForSeconds(ability.telegraphDuration);

            OnCastStarted?.Invoke(ability.abilityName, ability.castTime);
            if (ability.castTime > 0) yield return new WaitForSeconds(ability.castTime);

            // 4. Alive Check
            if (enemyAI != null && (enemyAI.Health.currentHealth <= 0 || !enemyAI.enabled))
            {
                yield break;
            }

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

        // Spawn Impact/Cast VFX
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
            case AbilityType.ForwardProjectile:
                HandleProjectile(ability, target);
                break;
            case AbilityType.GroundAOE:
                HandleGroundAOE(ability, position);
                break;
            case AbilityType.Self:
                HandleSelfCast(ability);
                break;
            case AbilityType.ChanneledBeam:
                HandleChanneledBeam(ability, target);
                break;
            case AbilityType.Leap:
                if (movementHandler != null) movementHandler.ExecuteLeap(position, () => HandleGroundAOE(ability, position));
                break;
            case AbilityType.Charge:
                if (enemyAI != null && target != null) enemyAI.ExecuteCharge(target, ability);
                break;
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

    // --- Helper to find body parts or fallback to Root ---
    private Transform GetAnchorTransform(VFXAnchor anchor)
    {
        // 1. Try explicit references (Drag and Drop in Inspector)
        if (anchor == VFXAnchor.LeftHand && leftHandAnchor != null) return leftHandAnchor;
        if (anchor == VFXAnchor.RightHand && rightHandAnchor != null) return rightHandAnchor;
        if (anchor == VFXAnchor.Head && headAnchor != null) return headAnchor;
        if (anchor == VFXAnchor.Feet && feetAnchor != null) return feetAnchor;
        if (anchor == VFXAnchor.Center && centerAnchor != null) return centerAnchor;

        // 2. Try Humanoid Animator references
        if (animator != null && animator.isHuman)
        {
            if (anchor == VFXAnchor.LeftHand) return animator.GetBoneTransform(HumanBodyBones.LeftHand) ?? transform;
            if (anchor == VFXAnchor.RightHand) return animator.GetBoneTransform(HumanBodyBones.RightHand) ?? transform;
            if (anchor == VFXAnchor.Head) return animator.GetBoneTransform(HumanBodyBones.Head) ?? transform;
            if (anchor == VFXAnchor.Center) return animator.GetBoneTransform(HumanBodyBones.Chest) ?? transform;
        }

        // 3. Fallback to ProjectileSpawnPoint (Mouth/WeaponTip) or Root
        return projectileSpawnPoint != null ? projectileSpawnPoint : transform;
    }

    private void HandleProjectile(Ability ability, GameObject target)
    {
        GameObject prefabToSpawn = ability.enemyProjectilePrefab != null ? ability.enemyProjectilePrefab : ability.playerProjectilePrefab;
        if (prefabToSpawn == null) return;

        // Determine Spawn Point
        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        Vector3 spawnPos = spawnTransform.position;

        // If aiming from the feet (root), lift it up slightly so it doesn't clip through the floor
        if (spawnTransform == transform) spawnPos += Vector3.up * 1.5f;

        Quaternion spawnRot = spawnTransform.rotation;

        if (target != null)
        {
            Vector3 targetPosition = target.transform.position;

            // Try to aim for center mass if a collider exists
            Collider targetCol = target.GetComponent<Collider>();
            if (targetCol != null) targetPosition = targetCol.bounds.center;

            Vector3 direction = targetPosition - spawnPos;

            // --- Planar Targeting (Y-Axis Flattening) ---
            direction.y = 0;
            // --------------------------------------------

            if (direction.sqrMagnitude > 0.001f)
                spawnRot = Quaternion.LookRotation(direction);
        }

        GameObject projectileGO = ObjectPooler.instance.Get(prefabToSpawn, spawnPos, spawnRot);
        if (projectileGO == null) return;

        // Setup Layer and Physics
        projectileGO.layer = LayerMask.NameToLayer("HostileRanged");
        Collider projectileCollider = projectileGO.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            Collider[] casterColliders = GetComponentInParent<CharacterRoot>().GetComponentsInChildren<Collider>();
            foreach (Collider c in casterColliders) Physics.IgnoreCollision(projectileCollider, c);
        }

        if (projectileGO.TryGetComponent<Projectile>(out var projectile))
        {
            int layer = GetComponentInParent<CharacterRoot>().gameObject.layer;
            projectile.Initialize(ability, this.gameObject, layer);
        }

        // Ensure the projectile is visible!
        projectileGO.SetActive(true);
    }

    // --- OTHER HANDLERS ---

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null)
        {
            GameObject vfx = ObjectPooler.instance.Get(ability.hitVFX, position, Quaternion.identity);
            if (vfx != null) vfx.SetActive(true);
        }

        int hitCount = Physics.OverlapSphereNonAlloc(position, ability.aoeRadius, hitBuffer, aoeTargetLayers);
        List<CharacterRoot> affectedCharacters = new List<CharacterRoot>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (Vector3.Distance(position, hit.transform.position) > ability.aoeRadius + 1.0f) continue;

            CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();
            if (hitCharacter == null || affectedCharacters.Contains(hitCharacter)) continue;

            affectedCharacters.Add(hitCharacter);
            CharacterRoot casterRoot = this.GetComponentInParent<CharacterRoot>();
            if (casterRoot == null) return;

            int casterLayer = casterRoot.gameObject.layer;
            int targetLayer = hitCharacter.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;

            var effectsToApply = isAlly ? ability.friendlyEffects : ability.hostileEffects;
            foreach (var effect in effectsToApply) effect.Apply(casterRoot.gameObject, hitCharacter.gameObject);
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

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, hitBuffer, transform.rotation);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit.transform.root == transform.root) continue;

            Health targetHealth = hit.GetComponentInChildren<Health>();
            if (targetHealth != null)
            {
                CharacterRoot myRoot = GetComponentInParent<CharacterRoot>();
                CharacterRoot targetRoot = hit.GetComponentInParent<CharacterRoot>();

                if (myRoot != null)
                {
                    int targetLayer = (targetRoot != null) ? targetRoot.gameObject.layer : hit.gameObject.layer;
                    if (myRoot.gameObject.layer != targetLayer)
                    {
                        GameObject targetObj = (targetRoot != null) ? targetRoot.gameObject : hit.gameObject;
                        foreach (var effect in ability.hostileEffects) effect.Apply(myRoot.gameObject, targetObj);
                    }
                }
            }
        }
    }

    private void PayCostAndStartCooldown(Ability ability, bool bypassCooldown)
    {
        if (!bypassCooldown)
        {
            cooldowns[ability] = Time.time + ability.cooldown;
            if (ability.triggersGlobalCooldown) globalCooldownTimer = Time.time + globalCooldownDuration;
        }
    }

    public bool CanUseAbility(Ability ability, GameObject target)
    {
        if (ability == null || IsCasting) return false;
        if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false;
        if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false;
        return true;
    }

    private void HandleChanneledBeam(Ability ability, GameObject target)
    {
        GameObject prefabToSpawn = ability.enemyProjectilePrefab != null ? ability.enemyProjectilePrefab : ability.playerProjectilePrefab;
        if (prefabToSpawn != null)
        {
            GameObject beamObject = Instantiate(prefabToSpawn, transform.position, transform.rotation);
            if (beamObject.TryGetComponent<ChanneledBeamController>(out var beam))
            {
                beam.Initialize(ability, this.gameObject, target);
                ActiveBeam = beam;
            }
        }
    }

    private void HandleSelfCast(Ability ability)
    {
        foreach (var effect in ability.friendlyEffects) effect.Apply(this.gameObject, this.gameObject);
    }

    void Update() { if (ActiveBeam != null && ActiveBeam.gameObject == null) { ActiveBeam = null; } }

    private void OnDrawGizmos()
    {
        if (debugDisplayTime > 0)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(debugBoxCenter, debugBoxRotation, debugBoxSize);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            debugDisplayTime -= Time.deltaTime;
        }
    }
}