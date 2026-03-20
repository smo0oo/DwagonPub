using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(AudioSource))]
public class PlayerAbilityHolder : MonoBehaviour
{
    public static event Action<PlayerAbilityHolder, Ability, GameObject, Vector3> OnPlayerAbilityUsed;
    public static event Action<float, float, Vector3> OnCameraShakeRequest;

    public static void TriggerCameraShake(float intensity, float duration, Vector3 position)
    {
        OnCameraShakeRequest?.Invoke(intensity, duration, position);
    }

    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;

    [Header("Component References")]
    [Tooltip("The default location projectiles spawn from (e.g. the player's chest).")]
    public Transform projectileSpawnPoint;
    private Transform defaultProjectileSpawnPoint;

    [Header("VFX Anchors (Optional Overrides)")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public Transform headAnchor;
    public Transform feetAnchor;
    public Transform centerAnchor;

    [Header("Animation Settings")]
    public string defaultAttackTrigger = "Attack";
    public string attackSpeedParam = "AttackSpeedMultiplier";
    [Tooltip("The boolean parameter that sustains channeling animations.")]
    public string channelingBoolParam = "IsChanneling";

    [Header("Targeting Settings")]
    public LayerMask targetLayers;

    [Header("Aim Magnetism & Bias (AAA)")]
    public bool enableAimMagnetism = true;
    public float magnetismAngle = 60f;
    public float magnetismBonusRange = 2f;

    [Tooltip("0 = Shoots exactly at cursor. 1 = Shoots exactly where player is facing. Blends between the two.")]
    [Range(0f, 1f)] public float aimOrientationBias = 0.0f;

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.0f;
    public float minGlobalCooldown = 0.5f;
    private float globalCooldownTimer = 0f;

    [Header("Game Feel")]
    public float inputBufferDuration = 0.4f;
    private float bufferExpirationTime = 0f;

    private Ability queuedAbility;
    private GameObject queuedTarget;
    private bool hasQueuedAction = false;

    private Ability lastBaseAbilityInput;
    private Ability currentComboNode;
    private float comboWindowEndTime = 0f;

    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;

    private Ability currentCastingAbility;
    private Ability currentExecutingAbility;

    private int currentStyleIndex = 0;
    private Quaternion currentAimRotation = Quaternion.identity;

    private GameObject currentExecutingTarget;
    private Vector3 currentExecutingPosition;
    private int currentProjectileIndex = 0;

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
    private Coroutine activeProjectileCoroutine;

    private GameObject currentCastingVFXInstance;
    private int attackHash;
    private int attackSpeedHash;
    private int attackStyleHash;
    private int castTriggerHash;
    private int forceIdleHash;
    private int isChannelingHash;
    private bool hasCastTrigger = false;
    private bool isAnimationLockedInternal = false;

    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;
    public bool IsAnimationLocked => isAnimationLockedInternal;

    public bool IsActivityLocked()
    {
        if (isAnimationLockedInternal) return true;
        if (IsCasting && currentCastingAbility != null && currentCastingAbility.locksPlayerActivity) return true;

        if (currentExecutingAbility != null && currentExecutingAbility.locksPlayerActivity)
        {
            bool isExecuting = activeProjectileCoroutine != null ||
                               activeMeleeCoroutine != null ||
                               activeLockCoroutine != null ||
                               ActiveBeam != null;
            if (isExecuting) return true;
        }
        return false;
    }

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
        forceIdleHash = Animator.StringToHash("ForceIdle");
        isChannelingHash = Animator.StringToHash(channelingBoolParam);

        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == "CastTrigger") { hasCastTrigger = true; break; }
            }
        }

        if (targetLayers.value == 0) targetLayers = LayerMask.GetMask("Default", "Player", "Enemy", "Destructible");

        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
        defaultProjectileSpawnPoint = projectileSpawnPoint;
    }

    public void SetDynamicProjectileSpawnPoint(Transform newSpawnPoint) { projectileSpawnPoint = newSpawnPoint; }
    public void ClearDynamicProjectileSpawnPoint() { projectileSpawnPoint = defaultProjectileSpawnPoint; }

    void Update()
    {
        if (hasQueuedAction)
        {
            if (Time.time > bufferExpirationTime) ClearInputBuffer();
            else if (!IsCasting && !isAnimationLockedInternal && !IsOnGlobalCooldown())
            {
                Ability ab = queuedAbility;
                GameObject tar = queuedTarget;
                ClearInputBuffer();
                UseAbility(ab, tar, false);
            }
        }
    }

    public void UseAbility(Ability ability, GameObject target) => UseAbility(ability, target, false);
    public void UseAbility(Ability ability, Vector3 targetPosition) => UseAbilityInternal(ability, null, targetPosition, false, ability, false);

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown)
    {
        if (ability == null) return;
        Ability originalInput = ability;
        bool isComboAdvance = false;

        if (ability == lastBaseAbilityInput && Time.time <= comboWindowEndTime && currentComboNode != null && currentComboNode.nextComboLink != null)
        {
            ability = currentComboNode.nextComboLink;
            if (currentComboNode.bypassGcdOnCombo) bypassCooldown = true;
            isComboAdvance = true;
        }

        Vector3 targetPosition;
        if (target != null) targetPosition = target.transform.position;
        else if (playerMovement != null)
        {
            targetPosition = playerMovement.CurrentGroundTarget;
            GameObject magneticTarget = FindMagneticTarget(ability, targetPosition);
            if (magneticTarget != null)
            {
                target = magneticTarget;
                targetPosition = magneticTarget.transform.position;
                Vector3 snapDir = (targetPosition - transform.position).normalized;
                snapDir.y = 0;
                if (snapDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(snapDir);
            }
        }
        else targetPosition = transform.position + transform.forward * 5f;

        UseAbilityInternal(ability, target, targetPosition, bypassCooldown, originalInput, isComboAdvance);
    }

    private void UseAbilityInternal(Ability ability, GameObject target, Vector3 position, bool bypassCooldown, Ability originalInput, bool isComboAdvance)
    {
        if (playerStats != null && playerStats.currentMana < ability.manaCost) return;
        if (!bypassCooldown && cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return;

        if (IsCasting || (!bypassCooldown && ability.triggersGlobalCooldown && IsOnGlobalCooldown()) || isAnimationLockedInternal)
        {
            QueueAbility(originalInput, target);
            return;
        }

        if (isComboAdvance) currentComboNode = ability;
        else
        {
            lastBaseAbilityInput = originalInput;
            currentComboNode = ability;
        }

        float finalCastTime = ability.castTime;
        if (playerStats != null) finalCastTime /= playerStats.secondaryStats.attackSpeed;

        comboWindowEndTime = Time.time + finalCastTime + ability.movementLockDuration + ability.comboWindow;

        int styleIndex = ability.attackStyleIndex;
        if (ability.randomizeAttackStyle && ability.maxRandomVariants > 0)
        {
            styleIndex = UnityEngine.Random.Range(0, ability.maxRandomVariants);
        }

        if (finalCastTime > 0 || ability.telegraphDuration > 0)
        {
            if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = StartCoroutine(PerformCast(ability, target, position, finalCastTime, bypassCooldown, styleIndex));
        }
        else
        {
            ExecuteAbility(ability, target, position, bypassCooldown, true, styleIndex);
        }
    }

    private GameObject FindMagneticTarget(Ability ability, Vector3 cursorPosition)
    {
        if (!enableAimMagnetism || ability == null) return null;
        if (ability.abilityType != AbilityType.TargetedMelee && ability.abilityType != AbilityType.DirectionalMelee && ability.abilityType != AbilityType.ForwardProjectile && ability.abilityType != AbilityType.TargetedProjectile && ability.abilityType != AbilityType.Grenade) return null;

        float searchRadius = ability.range + magnetismBonusRange;
        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, targetLayers);
        GameObject bestTarget = null;
        float bestScore = float.MaxValue;

        Vector3 cursorDir = (cursorPosition - transform.position).normalized;
        cursorDir.y = 0;
        if (cursorDir == Vector3.zero) cursorDir = transform.forward;

        foreach (Collider col in hits)
        {
            GameObject potentialTarget = null;
            CharacterRoot root = col.GetComponentInParent<CharacterRoot>();
            if (root != null) potentialTarget = root.gameObject;
            else { Health hp = col.GetComponentInParent<Health>(); if (hp != null) potentialTarget = hp.gameObject; }

            if (potentialTarget == null || potentialTarget == this.gameObject) continue;
            Health targetHp = potentialTarget.GetComponent<Health>();
            if (targetHp != null && targetHp.isDowned) continue;
            CharacterRoot myRoot = GetComponentInParent<CharacterRoot>();
            if (myRoot != null && root != null && myRoot.gameObject.layer == root.gameObject.layer) continue;

            Vector3 dirToTarget = (potentialTarget.transform.position - transform.position).normalized;
            dirToTarget.y = 0;
            float angle = Vector3.Angle(cursorDir, dirToTarget);

            if (angle <= magnetismAngle / 2f)
            {
                float distance = Vector3.Distance(transform.position, potentialTarget.transform.position);
                float score = distance + (angle * 0.1f);
                if (score < bestScore) { bestScore = score; bestTarget = potentialTarget; }
            }
        }
        return bestTarget;
    }

    private void QueueAbility(Ability ability, GameObject target) { queuedAbility = ability; queuedTarget = target; bufferExpirationTime = Time.time + inputBufferDuration; hasQueuedAction = true; }
    private void ClearInputBuffer() { hasQueuedAction = false; queuedAbility = null; queuedTarget = null; }

    private IEnumerator PerformCast(Ability ability, GameObject target, Vector3 position, float castTime, bool bypassCooldown, int styleIndex)
    {
        IsCasting = true;
        currentCastingAbility = ability;

        if (!ability.canMoveWhileCasting && navMeshAgent != null && navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath();

        if (animator != null)
        {
            animator.SetInteger(attackStyleHash, styleIndex);
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
                bool isFixedGroundTarget = ability.abilityType == AbilityType.GroundPlacement || ability.abilityType == AbilityType.GroundAOE || ability.abilityType == AbilityType.Leap || ability.abilityType == AbilityType.Teleport;
                if (!isFixedGroundTarget) position = playerMovement.CurrentGroundTarget;
            }

            ExecuteAbility(ability, target, position, bypassCooldown, true, styleIndex);
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

    private Vector3 GetCurrentShakeEpicenter(Ability ability, GameObject target, Vector3 position)
    {
        if (ability == null) return transform.position;

        switch (ability.screenShakeEpicenter)
        {
            case ScreenShakeEpicenter.Caster:
                return transform.position;
            case ScreenShakeEpicenter.TargetOrLocation:
                return (target != null) ? target.transform.position : position;
            case ScreenShakeEpicenter.GlobalCamera:
                return Camera.main != null ? Camera.main.transform.position : transform.position;
            default:
                return transform.position;
        }
    }

    private IEnumerator ExecuteScreenShake(Ability ability, GameObject target, Vector3 position)
    {
        float speedMultiplier = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;

        if (ability.screenShakeDelay > 0)
        {
            yield return new WaitForSeconds(ability.screenShakeDelay / speedMultiplier);
        }

        Vector3 shakePos = GetCurrentShakeEpicenter(ability, target, position);
        TriggerCameraShake(ability.screenShakeIntensity, ability.screenShakeDuration, shakePos);
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false, bool triggerAnimation = true, int styleIndex = 0)
    {
        currentExecutingAbility = ability;
        currentStyleIndex = styleIndex;

        currentExecutingTarget = target;
        currentExecutingPosition = position;
        currentProjectileIndex = 0;

        if (ActiveBeam != null) ActiveBeam.Interrupt();

        if (ability.abilityType != AbilityType.Charge) PayCostAndStartCooldown(ability, bypassCooldown);

        if (ability.castSound != null && ability.hitboxOpenDelay > 0) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);

        currentAimRotation = transform.rotation;
        if (ability.abilityType == AbilityType.ForwardProjectile || ability.abilityType == AbilityType.TargetedProjectile || ability.abilityType == AbilityType.Grenade)
        {
            Vector3 fireDir = (position - GetAnchorTransform(ability.castVFXAnchor).position).normalized;
            if (fireDir != Vector3.zero)
            {
                Vector3 blendedDir = Vector3.Slerp(fireDir, transform.forward, aimOrientationBias);
                currentAimRotation = Quaternion.LookRotation(blendedDir);
            }
        }

        bool hasVFXOverride = ability.styleVFXOverrides != null && ability.styleVFXOverrides.Count > styleIndex && ability.styleVFXOverrides[styleIndex].overrideVFX != null;
        if (ability.castVFX != null || hasVFXOverride)
        {
            if (ability.castVFXDelay > 0) StartCoroutine(SpawnVFXWithDelay(ability, currentAimRotation, styleIndex));
        }

        if (ability.screenShakeIntensity > 0)
        {
            StartCoroutine(ExecuteScreenShake(ability, target, position));
        }

        OnPlayerAbilityUsed?.Invoke(this, ability, target, position);

        if (ability.onCastEffects != null) { foreach (var effect in ability.onCastEffects) effect.Apply(gameObject, target); }

        if (triggerAnimation) TriggerAttackAnimation(ability, styleIndex);

        if (ability.movementLockDuration > 0)
        {
            if (activeLockCoroutine != null) StopCoroutine(activeLockCoroutine);
            activeLockCoroutine = StartCoroutine(HandleMovementLock(ability.movementLockDuration));
        }

        switch (ability.abilityType)
        {
            case AbilityType.TargetedProjectile:
            case AbilityType.ForwardProjectile:
            case AbilityType.Grenade:
                if (ability.useCoroutineForProjectiles)
                {
                    if (activeProjectileCoroutine != null) StopCoroutine(activeProjectileCoroutine);
                    activeProjectileCoroutine = StartCoroutine(ExecuteProjectileBurst(ability, target, position));
                }
                break;

            case AbilityType.TargetedMelee:
            case AbilityType.DirectionalMelee:
                if (activeMeleeCoroutine != null) { StopCoroutine(activeMeleeCoroutine); meleeHitbox.gameObject.SetActive(false); }
                meleeHitbox.Setup(ability, this.gameObject);
                BoxCollider collider = meleeHitbox.GetComponent<BoxCollider>();
                collider.size = ability.attackBoxSize;
                collider.center = ability.attackBoxCenter;

                if (ability.hitboxOpenDelay > 0) activeMeleeCoroutine = StartCoroutine(PerformMeleeAttackWithTimers(ability));
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

    public void OnAnimationEventOpenHitbox()
    {
        if (currentExecutingAbility == null) return;

        if (currentExecutingAbility.abilityType == AbilityType.TargetedMelee || currentExecutingAbility.abilityType == AbilityType.DirectionalMelee)
        {
            if (meleeHitbox != null) meleeHitbox.gameObject.SetActive(true);
        }
    }

    public void OnAnimationEventCloseHitbox()
    {
        if (meleeHitbox != null) meleeHitbox.gameObject.SetActive(false);
        activeMeleeCoroutine = null;
    }

    public void OnAnimationEventSpawnVFX()
    {
        if (currentExecutingAbility == null) return;
        SpawnCastVFX(currentExecutingAbility, currentAimRotation, currentStyleIndex);
    }

    public void OnAnimationEventPlayAudio()
    {
        if (currentExecutingAbility != null && currentExecutingAbility.castSound != null)
        {
            AudioSource.PlayClipAtPoint(currentExecutingAbility.castSound, transform.position);
        }
    }

    public void OnAnimationEventFireProjectile()
    {
        if (currentExecutingAbility == null) return;

        int totalCount = Mathf.Max(1, currentExecutingAbility.projectileCount);

        Vector3 targetPos = currentExecutingPosition;
        if (currentExecutingTarget == null && playerMovement != null) targetPos = playerMovement.CurrentGroundTarget;

        FireSingleProjectile(currentExecutingAbility, currentExecutingTarget, targetPos, currentProjectileIndex, totalCount);

        currentProjectileIndex++;
    }

    private IEnumerator ExecuteProjectileBurst(Ability ability, GameObject target, Vector3 initialTargetPos)
    {
        float speedMultiplier = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
        if (ability.projectileSpawnDelay > 0) yield return new WaitForSeconds(ability.projectileSpawnDelay / speedMultiplier);

        int count = Mathf.Max(1, ability.projectileCount);
        for (int i = 0; i < count; i++)
        {
            Health h = GetComponentInParent<Health>();
            if (h != null && h.isDowned) yield break;

            Vector3 currentTargetPos = initialTargetPos;
            if (target == null && playerMovement != null)
            {
                currentTargetPos = playerMovement.CurrentGroundTarget;
                GameObject magneticTarget = FindMagneticTarget(ability, currentTargetPos);
                if (magneticTarget != null)
                {
                    target = magneticTarget;
                    currentTargetPos = magneticTarget.transform.position;
                    Vector3 snapDir = (currentTargetPos - transform.position).normalized;
                    snapDir.y = 0;
                    if (snapDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(snapDir);
                }
            }

            FireSingleProjectile(ability, target, currentTargetPos, i, count);
            if (ability.burstDelay > 0 && i < count - 1) yield return new WaitForSeconds(ability.burstDelay / speedMultiplier);
        }
        activeProjectileCoroutine = null;
    }

    private void FireSingleProjectile(Ability ability, GameObject target, Vector3 targetPos, int index, int totalCount)
    {
        if (ability.projectilePrefab == null) return;

        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        Vector3 spawnPos = spawnTransform.position;
        Quaternion spawnRot = spawnTransform.rotation;

        Vector3 rawDirection = Vector3.zero;
        Vector3 trueTargetPos = targetPos;

        if (target != null)
        {
            Vector3 targetCenter = target.transform.position;
            Collider targetCollider = target.GetComponent<Collider>() ?? target.GetComponentInChildren<Collider>();
            if (targetCollider != null) targetCenter = targetCollider.bounds.center;

            rawDirection = (targetCenter - spawnPos).normalized;
            trueTargetPos = targetCenter;
        }
        else
        {
            float playerFloorY = transform.position.y;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit myHit, 2f, UnityEngine.AI.NavMesh.AllAreas)) playerFloorY = myHit.position.y;
            float spawnHeightAboveFloor = spawnPos.y - playerFloorY;
            Vector3 adjustedTargetPos = new Vector3(targetPos.x, targetPos.y + spawnHeightAboveFloor, targetPos.z);

            rawDirection = (adjustedTargetPos - spawnPos).normalized;
            trueTargetPos = adjustedTargetPos;
        }

        if (rawDirection.sqrMagnitude > 0.001f)
        {
            Vector3 blendedDirection = Vector3.Slerp(rawDirection, transform.forward, aimOrientationBias);
            spawnRot = Quaternion.LookRotation(blendedDirection);
        }

        if (ability.spreadAngle > 0 && totalCount > 1)
        {
            float angleOffset = 0f;
            if (ability.burstDelay == 0 && ability.useCoroutineForProjectiles)
            {
                float step = ability.spreadAngle / (totalCount - 1);
                float startAngle = -ability.spreadAngle / 2f;
                angleOffset = startAngle + (step * index);
            }
            else
            {
                angleOffset = UnityEngine.Random.Range(-ability.spreadAngle / 2f, ability.spreadAngle / 2f);
            }
            spawnRot *= Quaternion.Euler(0, angleOffset, 0);

            float dist = Vector3.Distance(spawnPos, trueTargetPos);
            trueTargetPos = spawnPos + (spawnRot * Vector3.forward) * dist;
        }

        GameObject projectileGO = ObjectPooler.instance.Get(ability.projectilePrefab, spawnPos, spawnRot);
        if (projectileGO != null)
        {
            int layerId = LayerMask.NameToLayer("FriendlyRanged");
            if (layerId != -1) projectileGO.layer = layerId;

            if (projectileGO.TryGetComponent<Projectile>(out var projectile))
            {
                CharacterRoot myRoot = GetComponentInParent<CharacterRoot>();
                int myLayer = myRoot != null ? myRoot.gameObject.layer : gameObject.layer;
                projectile.Initialize(ability, this.gameObject, myLayer, trueTargetPos);
            }

            Collider pCol = projectileGO.GetComponent<Collider>();
            if (pCol != null) { foreach (Collider c in GetComponentsInParent<Collider>()) Physics.IgnoreCollision(pCol, c); }
            projectileGO.SetActive(true);
        }
    }

    private void SpawnCastVFX(Ability ability, Quaternion? overrideRotation = null, int styleIndex = 0)
    {
        Transform anchor = GetAnchorTransform(ability.castVFXAnchor);
        Quaternion spawnRot = overrideRotation ?? anchor.rotation;
        GameObject vfxPrefab = ability.castVFX;
        Vector3 posOffset = ability.castVFXPositionOffset;
        Vector3 rotOffset = ability.castVFXRotationOffset;

        if (ability.styleVFXOverrides != null && styleIndex < ability.styleVFXOverrides.Count)
        {
            var styleOverride = ability.styleVFXOverrides[styleIndex];
            if (styleOverride.overrideVFX != null)
            {
                vfxPrefab = styleOverride.overrideVFX;
                posOffset = styleOverride.positionOffset;
                rotOffset = styleOverride.rotationOffset;
            }
        }

        if (vfxPrefab == null) return;

        GameObject vfxInstance = ObjectPooler.instance.Get(vfxPrefab, anchor.position, spawnRot);
        if (vfxInstance != null)
        {
            vfxInstance.transform.SetParent(anchor, false);
            vfxInstance.transform.localScale = vfxPrefab.transform.localScale;
            vfxInstance.transform.localPosition = posOffset;
            vfxInstance.transform.localRotation = Quaternion.Euler(rotOffset);
            vfxInstance.SetActive(true);
            if (!ability.attachCastVFX) vfxInstance.transform.SetParent(null);
        }
    }

    private Transform GetAnchorTransform(VFXAnchor anchor)
    {
        switch (anchor)
        {
            case VFXAnchor.LeftHand: if (leftHandAnchor != null) return leftHandAnchor; if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.LeftHand); if (bone != null) return bone; } break;
            case VFXAnchor.RightHand: if (rightHandAnchor != null) return rightHandAnchor; if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.RightHand); if (bone != null) return bone; } break;
            case VFXAnchor.Head: if (headAnchor != null) return headAnchor; if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.Head); if (bone != null) return bone; } break;
            case VFXAnchor.Feet: if (feetAnchor != null) return feetAnchor; return transform;
            case VFXAnchor.Center: if (centerAnchor != null) return centerAnchor; if (animator != null) { var bone = animator.GetBoneTransform(HumanBodyBones.Chest); if (bone != null) return bone; } return transform;
            case VFXAnchor.ProjectileSpawnPoint: default: return projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        }
        return transform;
    }

    private IEnumerator SpawnVFXWithDelay(Ability ability, Quaternion aimRot, int styleIndex)
    {
        float speedMultiplier = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
        yield return new WaitForSeconds(ability.castVFXDelay / speedMultiplier);
        SpawnCastVFX(ability, aimRot, styleIndex);
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
        float speedMultiplier = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
        yield return new WaitForSeconds(ability.hitboxOpenDelay / speedMultiplier);

        if (meleeHitbox != null) meleeHitbox.gameObject.SetActive(true);
        float duration = ability.hitboxCloseDelay - ability.hitboxOpenDelay;
        if (duration > 0) yield return new WaitForSeconds(duration / speedMultiplier);
        if (meleeHitbox != null) meleeHitbox.gameObject.SetActive(false);
        activeMeleeCoroutine = null;
    }

    private void TriggerAttackAnimation(Ability ability, int styleIndex)
    {
        if (animator != null)
        {
            float animSpeed = (playerStats != null) ? playerStats.secondaryStats.attackSpeed : 1f;
            animator.SetFloat(attackSpeedHash, animSpeed);
            animator.SetInteger(attackStyleHash, styleIndex);

            if (!string.IsNullOrEmpty(ability.overrideTriggerName))
            {
                animator.SetTrigger(ability.overrideTriggerName);
            }
            else
            {
                if (ability.abilityType != AbilityType.ChanneledBeam)
                {
                    animator.SetTrigger(attackHash);
                }
            }
        }
    }

    public void CancelCast(bool isMovementInterrupt = false)
    {
        bool isBusy = IsCasting || isAnimationLockedInternal || activeCastCoroutine != null || activeProjectileCoroutine != null || activeMeleeCoroutine != null || activeLockCoroutine != null || ActiveBeam != null;
        if (!isBusy) return;

        if (isMovementInterrupt)
        {
            if (IsCasting && currentCastingAbility != null && currentCastingAbility.canMoveWhileCasting) return;
            if (IsActivityLocked()) return;
        }

        if (animator != null)
        {
            animator.ResetTrigger(attackHash);
            if (hasCastTrigger) animator.ResetTrigger(castTriggerHash);
            animator.SetInteger(attackStyleHash, -1);
            animator.SetBool(isChannelingHash, false);

            if (currentCastingAbility != null)
            {
                if (!string.IsNullOrEmpty(currentCastingAbility.telegraphAnimationTrigger)) animator.ResetTrigger(currentCastingAbility.telegraphAnimationTrigger);
                if (!string.IsNullOrEmpty(currentCastingAbility.overrideTriggerName)) animator.ResetTrigger(currentCastingAbility.overrideTriggerName);
            }

            if (currentExecutingAbility != null && !string.IsNullOrEmpty(currentExecutingAbility.overrideTriggerName)) animator.ResetTrigger(currentExecutingAbility.overrideTriggerName);
            animator.SetTrigger(forceIdleHash);
        }

        if (activeCastCoroutine != null) { StopCoroutine(activeCastCoroutine); activeCastCoroutine = null; }
        if (activeProjectileCoroutine != null) { StopCoroutine(activeProjectileCoroutine); activeProjectileCoroutine = null; }
        if (activeLockCoroutine != null) { StopCoroutine(activeLockCoroutine); activeLockCoroutine = null; }
        if (activeMeleeCoroutine != null) { StopCoroutine(activeMeleeCoroutine); activeMeleeCoroutine = null; if (meleeHitbox != null) meleeHitbox.gameObject.SetActive(false); }
        if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }

        IsCasting = false;
        currentCastingAbility = null;
        currentExecutingAbility = null;
        lastBaseAbilityInput = null;
        currentComboNode = null;
        comboWindowEndTime = 0f;

        CleanupCastingVFX();
        ClearInputBuffer();
        isAnimationLockedInternal = false;

        if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StopCast();
        if (ActiveBeam != null) { ActiveBeam.Interrupt(); ActiveBeam = null; }
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

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null)
        {
            Vector3 spawnPos = position + ability.hitVFXPositionOffset;
            Quaternion spawnRot = Quaternion.Euler(ability.hitVFXRotationOffset);
            GameObject vfx = ObjectPooler.instance.Get(ability.hitVFX, spawnPos, spawnRot);
            if (vfx != null) { vfx.transform.localScale = ability.hitVFX.transform.localScale; vfx.SetActive(true); }
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
            else { Health hitHealth = col.GetComponentInParent<Health>(); if (hitHealth != null) finalTarget = hitHealth.gameObject; }

            if (finalTarget != null && !hitTargets.Contains(finalTarget))
            {
                hitTargets.Add(finalTarget);
                bool isAlly = false;
                if (hitCharacter != null) { CharacterRoot myRoot = GetComponentInParent<CharacterRoot>(); if (myRoot != null) isAlly = myRoot.gameObject.layer == hitCharacter.gameObject.layer; }
                List<IAbilityEffect> effectsToApply = isAlly ? ability.friendlyEffects : ability.hostileEffects;
                foreach (var effect in effectsToApply) effect.Apply(gameObject, finalTarget);
            }
        }
    }

    private void HandleSelfCast(Ability ability)
    {
        CharacterRoot casterRoot = GetComponentInParent<CharacterRoot>();
        if (casterRoot == null) return;
        if (ability.impactSound != null) AudioSource.PlayClipAtPoint(ability.impactSound, transform.position);
        if (ability.aoeRadius <= 0) { foreach (var effect in ability.friendlyEffects) effect.Apply(casterRoot.gameObject, casterRoot.gameObject); }
        else HandleGroundAOE(ability, transform.position);
    }

    private void HandleChanneledBeam(Ability ability, GameObject target)
    {
        GameObject prefabToSpawn = ability.channeledBeamPrefab;
        if (prefabToSpawn != null)
        {
            Transform anchor = GetAnchorTransform(ability.channeledBeamAnchor);
            GameObject beamObject = Instantiate(prefabToSpawn, anchor.position, anchor.rotation, anchor);

            if (beamObject.TryGetComponent<ChanneledBeamController>(out var beam))
            {
                beam.Initialize(ability, this.gameObject, target, anchor);
                ActiveBeam = beam;
                if (animator != null) animator.SetBool(isChannelingHash, true);
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

            if (placedObject.TryGetComponent<AreaBombardmentController>(out var bombardment)) bombardment.Initialize(caster, ability);
            else if (placedObject.TryGetComponent<PlaceableTrap>(out var trap)) trap.owner = caster;
        }
    }

    public bool GetCooldownStatus(Ability ability, out float remaining)
    {
        if (ability == lastBaseAbilityInput && Time.time <= comboWindowEndTime && currentComboNode != null && currentComboNode.nextComboLink != null) ability = currentComboNode.nextComboLink;
        remaining = 0f;
        if (cooldowns.TryGetValue(ability, out float endTime)) { if (Time.time < endTime) { remaining = endTime - Time.time; return true; } }
        return false;
    }

    public bool CanUseAbility(Ability ability, GameObject target)
    {
        if (ability == lastBaseAbilityInput && Time.time <= comboWindowEndTime && currentComboNode != null && currentComboNode.nextComboLink != null) ability = currentComboNode.nextComboLink;
        if (ability == null || IsCasting) return false;
        if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false;
        if (ability.requiresWeaponType && !IsCorrectWeaponEquipped(ability.requiredWeaponCategories)) return false;
        if ((ability.abilityType == AbilityType.TargetedMelee || ability.abilityType == AbilityType.TargetedProjectile || ability.abilityType == AbilityType.Charge) && target == null) return false;
        if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false;
        if (playerStats != null && playerStats.currentMana < ability.manaCost) return false;
        return true;
    }

    public bool IsCorrectWeaponEquipped(List<ItemWeaponStats.WeaponCategory> categories) { if (playerEquipment == null) return false; if (categories == null || categories.Count == 0) return false; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem) && rightHandItem?.itemData?.stats is ItemWeaponStats rightWeapon && categories.Contains(rightWeapon.weaponCategory)) return true; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem) && leftHandItem?.itemData?.stats is ItemWeaponStats leftWeapon && categories.Contains(leftWeapon.weaponCategory)) return true; return false; }
}