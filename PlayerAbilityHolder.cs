using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(AudioSource))]
public class PlayerAbilityHolder : MonoBehaviour
{
    // --- AAA FIX: Added Target and Position to the payload so enemies can filter efficiently ---
    public static event Action<PlayerAbilityHolder, Ability, GameObject, Vector3> OnPlayerAbilityUsed;

    public static event Action<float, float, Vector3> OnCameraShakeRequest;

    // --- AAA FIX: Wrapper to allow enemies to shake the camera ---
    public static void TriggerCameraShake(float intensity, float duration, Vector3 position)
    {
        OnCameraShakeRequest?.Invoke(intensity, duration, position);
    }
    // -------------------------------------------------------------

    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;

    [Header("Component References")]
    public Transform projectileSpawnPoint;

    [Header("VFX Anchors (Optional Overrides)")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public Transform headAnchor;
    public Transform feetAnchor;
    public Transform centerAnchor;

    [Header("Animation Settings")]
    public string defaultAttackTrigger = "Attack";
    public string attackSpeedParam = "AttackSpeedMultiplier";

    [Header("Targeting Settings")]
    public LayerMask targetLayers;

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.0f;
    public float minGlobalCooldown = 0.5f;
    private float globalCooldownTimer = 0f;

    [Header("Game Feel")]
    public float inputBufferDuration = 0.4f;
    private float bufferExpirationTime = 0f;

    private Ability queuedAbility;
    private GameObject queuedTarget;
    private Vector3 queuedPosition;
    private bool hasQueuedAction = false;

    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;
    private Ability currentCastingAbility;

    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private PlayerStats playerStats;
    private IMovementHandler movementHandler;
    private PlayerMovement playerMovement;
    private MeleeHitbox meleeHitbox;
    private PlayerEquipment playerEquipment;
    private UnityEngine.AI.NavMeshAgent navMeshAgent;
    private Animator animator;
    private AudioSource audioSource;

    private Coroutine activeCastCoroutine;
    private Coroutine activeMeleeCoroutine;
    private Coroutine activeLockCoroutine;

    private GameObject currentCastingVFXInstance;
    private int attackHash;
    private int attackSpeedHash;
    private int attackStyleHash;
    private int castTriggerHash;
    private bool hasCastTrigger = false;
    private bool isAnimationLockedInternal = false;

    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;
    public bool IsAnimationLocked => isAnimationLockedInternal;

    void Awake()
    {
        CharacterRoot characterRoot = GetComponentInParent<CharacterRoot>();
        if (characterRoot != null)
        {
            playerStats = characterRoot.PlayerStats;
            playerEquipment = characterRoot.PlayerEquipment;
            navMeshAgent = characterRoot.GetComponent<UnityEngine.AI.NavMeshAgent>();
            animator = characterRoot.Animator;
            playerMovement = characterRoot.PlayerMovement;
        }
        else
        {
            animator = GetComponentInChildren<Animator>();
        }

        audioSource = GetComponent<AudioSource>();
        movementHandler = GetComponentInParent<IMovementHandler>();
        meleeHitbox = GetComponentInChildren<MeleeHitbox>(true);

        attackHash = Animator.StringToHash(defaultAttackTrigger);
        attackSpeedHash = Animator.StringToHash(attackSpeedParam);
        attackStyleHash = Animator.StringToHash("AttackStyle");
        castTriggerHash = Animator.StringToHash("CastTrigger");

        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == "CastTrigger") { hasCastTrigger = true; break; }
            }
        }

        if (targetLayers.value == 0) targetLayers = LayerMask.GetMask("Default", "Player", "Enemy", "Destructible");
        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
    }

    void Update()
    {
        if (hasQueuedAction)
        {
            if (Time.time > bufferExpirationTime) ClearInputBuffer();
            else if (!IsCasting && !isAnimationLockedInternal && !IsOnGlobalCooldown())
            {
                Ability ab = queuedAbility;
                GameObject tar = queuedTarget;
                Vector3 pos = queuedPosition;
                ClearInputBuffer();
                if (CanUseAbility(ab, tar)) UseAbility(ab, tar, pos, false);
            }
        }
    }

    public void UseAbility(Ability ability, GameObject target) => UseAbility(ability, target, false);
    public void UseAbility(Ability ability, Vector3 targetPosition) => UseAbility(ability, null, targetPosition, false);

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown)
    {
        Vector3 targetPosition;
        if (target != null) targetPosition = target.transform.position;
        else if (playerMovement != null) targetPosition = playerMovement.CurrentLookTarget;
        else targetPosition = transform.position + transform.forward * 5f;

        UseAbility(ability, target, targetPosition, bypassCooldown);
    }

    private void UseAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown)
    {
        if (ability == null) return;
        if (playerStats != null && playerStats.currentMana < ability.manaCost) return;
        if (!bypassCooldown && cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return;

        if (IsCasting || (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) || isAnimationLockedInternal)
        {
            QueueAbility(ability, target, position);
            return;
        }

        float finalCastTime = ability.castTime;
        if (playerStats != null) finalCastTime /= playerStats.secondaryStats.attackSpeed;

        if (finalCastTime > 0 || ability.telegraphDuration > 0)
        {
            if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = StartCoroutine(PerformCast(ability, target, position, finalCastTime, bypassCooldown));
        }
        else
        {
            ExecuteAbility(ability, target, position, bypassCooldown);
        }
    }

    private void QueueAbility(Ability ability, GameObject target, Vector3 position)
    {
        queuedAbility = ability;
        queuedTarget = target;
        queuedPosition = position;
        bufferExpirationTime = Time.time + inputBufferDuration;
        hasQueuedAction = true;
    }

    private void ClearInputBuffer() { hasQueuedAction = false; queuedAbility = null; queuedTarget = null; }

    private IEnumerator PerformCast(Ability ability, GameObject target, Vector3 position, float castTime, bool bypassCooldown)
    {
        IsCasting = true;
        currentCastingAbility = ability;

        if (!ability.canMoveWhileCasting && navMeshAgent != null && navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath();

        if (animator != null)
        {
            // PHASE 1: WIND-UP / CASTING (Strictly uses telegraphAnimationTrigger or castTriggerHash)
            if (!string.IsNullOrEmpty(ability.telegraphAnimationTrigger)) animator.SetTrigger(ability.telegraphAnimationTrigger);
            else if (hasCastTrigger) animator.SetTrigger(castTriggerHash);
        }

        if (ability.windupSound != null && audioSource != null)
        {
            audioSource.clip = ability.windupSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (ability.castingVFX != null)
        {
            Transform anchor = GetAnchorTransform(ability.castingVFXAnchor);
            currentCastingVFXInstance = ObjectPooler.instance.Get(ability.castingVFX, anchor.position, anchor.rotation);
            if (currentCastingVFXInstance != null)
            {
                currentCastingVFXInstance.transform.SetParent(anchor, false);
                currentCastingVFXInstance.transform.localPosition = ability.castingVFXPositionOffset;
                currentCastingVFXInstance.transform.localRotation = Quaternion.Euler(ability.castingVFXRotationOffset);
                currentCastingVFXInstance.transform.localScale = ability.castingVFX.transform.localScale;
                currentCastingVFXInstance.SetActive(true);

                if (!ability.attachCastingVFX) currentCastingVFXInstance.transform.SetParent(null);
            }
        }

        OnCastStarted?.Invoke(ability.abilityName, castTime);

        try
        {
            if (ability.showCastBar && CastingBarUIManager.instance != null) CastingBarUIManager.instance.StartCast(ability.abilityName, castTime);
            if (ability.telegraphDuration > 0) yield return new WaitForSeconds(ability.telegraphDuration);
            if (castTime > 0) yield return new WaitForSeconds(castTime);

            if (target == null && playerMovement != null)
            {
                bool isFixedGroundTarget = ability.abilityType == AbilityType.GroundPlacement ||
                                           ability.abilityType == AbilityType.GroundAOE ||
                                           ability.abilityType == AbilityType.Leap ||
                                           ability.abilityType == AbilityType.Teleport;

                if (!isFixedGroundTarget)
                {
                    position = playerMovement.CurrentLookTarget;
                }
            }

            ExecuteAbility(ability, target, position, bypassCooldown, (ability.abilityType != AbilityType.ChanneledBeam));
        }
        finally
        {
            CleanupCastingVFX();
            if (audioSource != null && audioSource.clip == ability.windupSound) { audioSource.Stop(); audioSource.loop = false; audioSource.clip = null; }
            IsCasting = false;
            currentCastingAbility = null;
            activeCastCoroutine = null;
            if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StopCast();
            OnCastFinished?.Invoke();
        }
    }

    private void CleanupCastingVFX()
    {
        if (currentCastingVFXInstance != null)
        {
            VFXGraphCleaner[] cleaners = currentCastingVFXInstance.GetComponentsInChildren<VFXGraphCleaner>();
            if (cleaners.Length > 0) foreach (var c in cleaners) c.StopAndFade();
            else
            {
                PooledObject pooled = currentCastingVFXInstance.GetComponentInParent<PooledObject>();
                if (pooled != null) pooled.ReturnToPool();
                else Destroy(currentCastingVFXInstance);
            }
            currentCastingVFXInstance = null;
        }
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false, bool triggerAnimation = true)
    {
        if (ActiveBeam != null) ActiveBeam.Interrupt();
        if (ability.abilityType != AbilityType.Charge) PayCostAndStartCooldown(ability, bypassCooldown);
        if (ability.castSound != null) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);

        Quaternion aimRotation = transform.rotation;
        if (ability.abilityType == AbilityType.ForwardProjectile || ability.abilityType == AbilityType.TargetedProjectile)
        {
            Vector3 fireDir = (position - GetAnchorTransform(ability.castVFXAnchor).position).normalized;
            if (fireDir != Vector3.zero) aimRotation = Quaternion.LookRotation(fireDir);
        }

        if (ability.castVFX != null)
        {
            if (ability.castVFXDelay > 0) StartCoroutine(SpawnVFXWithDelay(ability, aimRotation));
            else SpawnCastVFX(ability, aimRotation);
        }

        if (ability.screenShakeIntensity > 0)
            OnCameraShakeRequest?.Invoke(ability.screenShakeIntensity, ability.screenShakeDuration, transform.position);

        OnPlayerAbilityUsed?.Invoke(this, ability, target, position);

        if (triggerAnimation) TriggerAttackAnimation(ability);

        if (ability.movementLockDuration > 0)
        {
            if (activeLockCoroutine != null) StopCoroutine(activeLockCoroutine);
            activeLockCoroutine = StartCoroutine(HandleMovementLock(ability.movementLockDuration));
        }

        switch (ability.abilityType)
        {
            case AbilityType.TargetedProjectile:
            case AbilityType.ForwardProjectile: HandleProjectile(ability, target, position); break;
            case AbilityType.TargetedMelee:
            case AbilityType.DirectionalMelee:
                if (activeMeleeCoroutine != null) { StopCoroutine(activeMeleeCoroutine); meleeHitbox.gameObject.SetActive(false); }
                activeMeleeCoroutine = StartCoroutine(PerformMeleeAttackWithTimers(ability));
                break;
            case AbilityType.GroundAOE: HandleGroundAOE(ability, position); break;
            case AbilityType.Self: HandleSelfCast(ability); break;
            case AbilityType.GroundPlacement: HandleGroundPlacement(ability, position); break;
            case AbilityType.ChanneledBeam: HandleChanneledBeam(ability, target); break;
            case AbilityType.Charge: if (playerMovement != null) playerMovement.InitiateCharge(target, ability); break;
            case AbilityType.Leap: if (movementHandler != null) movementHandler.ExecuteLeap(position, () => { HandleGroundAOE(ability, position); }); break;
            case AbilityType.Teleport: if (movementHandler != null) movementHandler.ExecuteTeleport(position); break;
        }
    }

    private void SpawnCastVFX(Ability ability, Quaternion? overrideRotation = null)
    {
        Transform anchor = GetAnchorTransform(ability.castVFXAnchor);
        Quaternion spawnRot = overrideRotation ?? anchor.rotation;

        GameObject vfxInstance = ObjectPooler.instance.Get(ability.castVFX, anchor.position, spawnRot);
        if (vfxInstance != null)
        {
            vfxInstance.transform.SetParent(anchor, false);
            vfxInstance.transform.localScale = ability.castVFX.transform.localScale;
            vfxInstance.transform.localPosition = ability.castVFXPositionOffset;
            vfxInstance.transform.localRotation = Quaternion.Euler(ability.castVFXRotationOffset);
            vfxInstance.SetActive(true);

            if (!ability.attachCastVFX) vfxInstance.transform.SetParent(null);
        }
    }

    private Transform GetAnchorTransform(VFXAnchor anchor)
    {
        switch (anchor)
        {
            case VFXAnchor.LeftHand:
                if (leftHandAnchor != null) return leftHandAnchor;
                if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.LeftHand); if (bone != null) return bone; }
                break;
            case VFXAnchor.RightHand:
                if (rightHandAnchor != null) return rightHandAnchor;
                if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.RightHand); if (bone != null) return bone; }
                break;
            case VFXAnchor.Head:
                if (headAnchor != null) return headAnchor;
                if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.Head); if (bone != null) return bone; }
                break;
            case VFXAnchor.Feet:
                if (feetAnchor != null) return feetAnchor;
                return transform;
            case VFXAnchor.Center:
                if (centerAnchor != null) return centerAnchor;
                if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.Chest); if (bone != null) return bone; }
                return transform;
            case VFXAnchor.ProjectileSpawnPoint:
            default:
                return projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        }
        return transform;
    }

    private IEnumerator SpawnVFXWithDelay(Ability ability, Quaternion aimRot)
    {
        float speedMultiplier = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
        yield return new WaitForSeconds(ability.castVFXDelay / speedMultiplier);
        SpawnCastVFX(ability, aimRot);
    }

    private IEnumerator HandleMovementLock(float baseDuration)
    {
        isAnimationLockedInternal = true;
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh) { navMeshAgent.ResetPath(); navMeshAgent.velocity = Vector3.zero; }
        float speedMultiplier = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
        yield return new WaitForSeconds(baseDuration / speedMultiplier);
        isAnimationLockedInternal = false;
    }

    private IEnumerator PerformMeleeAttackWithTimers(Ability ability)
    {
        meleeHitbox.Setup(ability, this.gameObject);
        BoxCollider collider = meleeHitbox.GetComponent<BoxCollider>();
        collider.size = ability.attackBoxSize;
        collider.center = ability.attackBoxCenter;
        float speedMultiplier = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
        yield return new WaitForSeconds(ability.hitboxOpenDelay / speedMultiplier);
        meleeHitbox.gameObject.SetActive(true);
        float duration = ability.hitboxCloseDelay - ability.hitboxOpenDelay;
        if (duration > 0) yield return new WaitForSeconds(duration / speedMultiplier);
        meleeHitbox.gameObject.SetActive(false);
        activeMeleeCoroutine = null;
    }

    private void TriggerAttackAnimation(Ability ability)
    {
        if (animator != null)
        {
            animator.ResetTrigger(attackHash);
            float animSpeed = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
            animator.SetFloat(attackSpeedHash, animSpeed);

            // PHASE 2: EXECUTION / ATTACK (Strictly uses overrideTriggerName, otherwise defaults to standard Attack combo)
            if (!string.IsNullOrEmpty(ability.overrideTriggerName)) animator.SetTrigger(ability.overrideTriggerName);
            else { animator.SetInteger(attackStyleHash, ability.attackStyleIndex); animator.SetTrigger(attackHash); }
        }
    }

    public void CancelCast(bool isMovementInterrupt = false)
    {
        if (isMovementInterrupt && IsCasting && currentCastingAbility != null && currentCastingAbility.canMoveWhileCasting) return;
        if (activeCastCoroutine != null) { StopCoroutine(activeCastCoroutine); activeCastCoroutine = null; }
        if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }
        IsCasting = false;
        currentCastingAbility = null;
        CleanupCastingVFX();
        ClearInputBuffer();
        isAnimationLockedInternal = false;
        if (activeLockCoroutine != null) { StopCoroutine(activeLockCoroutine); activeLockCoroutine = null; }
        if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StopCast();
        if (ActiveBeam != null) { ActiveBeam.Interrupt(); ActiveBeam = null; }
        if (activeMeleeCoroutine != null) { StopCoroutine(activeMeleeCoroutine); activeMeleeCoroutine = null; if (meleeHitbox != null) meleeHitbox.gameObject.SetActive(false); }
    }

    public void PayCostAndStartCooldown(Ability ability, bool bypassCooldown = false)
    {
        if (playerStats != null) { playerStats.SpendMana(ability.manaCost); }
        if (!bypassCooldown)
        {
            if (ability.abilityType != AbilityType.ChanneledBeam)
            {
                float baseCooldown = (playerMovement != null && ability == playerMovement.defaultAttackAbility) ? GetCurrentWeaponSpeed() : ability.cooldown;
                float finalCooldown = baseCooldown / (playerStats != null ? playerStats.secondaryStats.attackSpeed : 1f);
                if (playerStats != null) finalCooldown *= playerStats.secondaryStats.cooldownReduction;
                cooldowns[ability] = Time.time + finalCooldown;
            }
            if (ability.triggersGlobalCooldown)
            {
                float effectiveGcd = globalCooldownDuration / (playerStats != null ? playerStats.secondaryStats.attackSpeed : 1f);
                globalCooldownTimer = Time.time + Mathf.Max(minGlobalCooldown, effectiveGcd);
            }
        }
    }

    private float GetCurrentWeaponSpeed() { if (playerEquipment == null) return 2.0f; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem) && rightHandItem?.itemData.stats is ItemWeaponStats rightWeapon) return rightWeapon.baseAttackTime; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem) && leftHandItem?.itemData.stats is ItemWeaponStats leftWeapon) return leftWeapon.baseAttackTime; return 2.0f; }

    private void HandleProjectile(Ability ability, GameObject target, Vector3 targetPos)
    {
        if (ability.playerProjectilePrefab == null) return;
        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        Vector3 spawnPos = spawnTransform.position;
        Quaternion spawnRot = spawnTransform.rotation;

        if (target != null)
        {
            Vector3 targetCenter = target.transform.position;
            Collider targetCollider = target.GetComponent<Collider>() ?? target.GetComponentInChildren<Collider>();
            if (targetCollider != null) targetCenter = targetCollider.bounds.center;
            Vector3 direction = targetCenter - spawnPos;
            if (direction != Vector3.zero) spawnRot = Quaternion.LookRotation(direction);
        }
        else
        {
            Vector3 direction = targetPos - spawnPos;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f) spawnRot = Quaternion.LookRotation(direction);
        }

        GameObject projectileGO = ObjectPooler.instance.Get(ability.playerProjectilePrefab, spawnPos, spawnRot);
        if (projectileGO != null)
        {
            projectileGO.layer = LayerMask.NameToLayer("FriendlyRanged");
            projectileGO.SetActive(true);
            if (projectileGO.TryGetComponent<Projectile>(out var projectile)) projectile.Initialize(ability, this.gameObject, GetComponentInParent<CharacterRoot>().gameObject.layer);
        }
    }

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null)
        {
            Vector3 spawnPos = position + ability.hitVFXPositionOffset;
            Quaternion spawnRot = Quaternion.Euler(ability.hitVFXRotationOffset);
            GameObject vfx = ObjectPooler.instance.Get(ability.hitVFX, spawnPos, spawnRot);
            if (vfx != null)
            {
                vfx.transform.localScale = ability.hitVFX.transform.localScale;
                vfx.SetActive(true);
            }
        }

        if (ability.impactSound != null) AudioSource.PlayClipAtPoint(ability.impactSound, position);

        Collider[] _aoeBuffer = new Collider[100];
        int hitCount = Physics.OverlapSphereNonAlloc(position, ability.aoeRadius, _aoeBuffer, targetLayers);
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _aoeBuffer[i];
            GameObject finalTarget = null;
            CharacterRoot hitCharacter = col.GetComponentInParent<CharacterRoot>();

            if (hitCharacter != null) finalTarget = hitCharacter.gameObject;
            else
            {
                Health hitHealth = col.GetComponentInParent<Health>();
                if (hitHealth != null) finalTarget = hitHealth.gameObject;
            }

            if (finalTarget != null && !hitTargets.Contains(finalTarget))
            {
                hitTargets.Add(finalTarget);
                bool isAlly = false;

                if (hitCharacter != null)
                {
                    CharacterRoot myRoot = GetComponentInParent<CharacterRoot>();
                    if (myRoot != null) isAlly = myRoot.gameObject.layer == hitCharacter.gameObject.layer;
                }

                List<IAbilityEffect> effectsToApply = isAlly ? ability.friendlyEffects : ability.hostileEffects;
                foreach (var effect in effectsToApply)
                {
                    effect.Apply(gameObject, finalTarget);
                }
            }
        }
    }

    private void HandleSelfCast(Ability ability)
    {
        CharacterRoot casterRoot = GetComponentInParent<CharacterRoot>();
        if (casterRoot == null) return;
        if (ability.impactSound != null) AudioSource.PlayClipAtPoint(ability.impactSound, transform.position);
        if (ability.aoeRadius <= 0) { foreach (var effect in ability.friendlyEffects) effect.Apply(casterRoot.gameObject, casterRoot.gameObject); }
        else { HandleGroundAOE(ability, transform.position); }
    }

    private void HandleChanneledBeam(Ability ability, GameObject target)
    {
        GameObject prefabToSpawn = ability.playerProjectilePrefab ?? ability.enemyProjectilePrefab;
        if (prefabToSpawn != null)
        {
            GameObject beamObject = Instantiate(prefabToSpawn, transform.position, transform.rotation, transform);
            if (beamObject.TryGetComponent<ChanneledBeamController>(out var beam))
            {
                beam.Initialize(ability, this.gameObject, target, projectileSpawnPoint);
                ActiveBeam = beam;
            }
        }
    }

    private void HandleGroundPlacement(Ability ability, Vector3 position)
    {
        if (ability.placementPrefab != null)
        {
            GameObject placedObject = Instantiate(ability.placementPrefab, position, Quaternion.identity);
            GameObject caster = this.gameObject;
            CharacterRoot casterRoot = GetComponentInParent<CharacterRoot>();
            if (casterRoot != null) caster = casterRoot.gameObject;

            if (placedObject.TryGetComponent<AreaBombardmentController>(out var bombardment))
            {
                bombardment.Initialize(caster, ability);
            }
            else if (placedObject.TryGetComponent<PlaceableTrap>(out var trap))
            {
                trap.owner = caster;
            }
        }
    }

    public bool GetCooldownStatus(Ability ability, out float remaining) { remaining = 0f; if (cooldowns.TryGetValue(ability, out float endTime)) { if (Time.time < endTime) { remaining = endTime - Time.time; return true; } } return false; }
    public bool CanUseAbility(Ability ability, GameObject target) { if (ability == null || IsCasting) return false; if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false; if (ability.requiresWeaponType && !IsCorrectWeaponEquipped(ability.requiredWeaponCategories)) return false; if ((ability.abilityType == AbilityType.TargetedMelee || ability.abilityType == AbilityType.TargetedProjectile || ability.abilityType == AbilityType.Charge) && target == null) return false; if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false; if (playerStats != null && playerStats.currentMana < ability.manaCost) return false; return true; }
    public bool IsCorrectWeaponEquipped(List<ItemWeaponStats.WeaponCategory> categories) { if (playerEquipment == null) return false; if (categories == null || categories.Count == 0) return false; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem) && rightHandItem?.itemData?.stats is ItemWeaponStats rightWeapon && categories.Contains(rightWeapon.weaponCategory)) return true; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem) && leftHandItem?.itemData?.stats is ItemWeaponStats leftWeapon && categories.Contains(leftWeapon.weaponCategory)) return true; return false; }
}