using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(AudioSource))] // Added for better audio control
public class PlayerAbilityHolder : MonoBehaviour
{
    public static event Action<PlayerAbilityHolder, Ability> OnPlayerAbilityUsed;

    // New Event for Camera Controller to subscribe to
    public static event Action<float, float> OnCameraShakeRequest;

    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;

    [Header("Component References")]
    [Tooltip("An optional transform to specify where projectiles should spawn from. If not set, the character's pivot point is used.")]
    public Transform projectileSpawnPoint;

    [Header("Animation Settings")]
    public string defaultAttackTrigger = "Attack";
    public string attackSpeedParam = "AttackSpeedMultiplier";

    [Header("Targeting Settings")]
    public LayerMask targetLayers;

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.0f;
    public float minGlobalCooldown = 0.5f;
    private float globalCooldownTimer = 0f;

    [Header("Game Feel (Input Buffering)")]
    public float inputBufferDuration = 0.4f;
    private float bufferExpirationTime = 0f;

    private Ability queuedAbility;
    private GameObject queuedTarget;
    private Vector3 queuedPosition;
    private bool hasQueuedAction = false;

    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;
    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;
    public bool IsAnimationLocked { get; private set; } = false;

    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private PlayerStats playerStats;
    private IMovementHandler movementHandler;
    private PlayerMovement playerMovement;
    private MeleeHitbox meleeHitbox;
    private PlayerEquipment playerEquipment;
    private UnityEngine.AI.NavMeshAgent navMeshAgent;
    private Animator animator;
    private AudioSource audioSource; // For windups/channeling

    private Coroutine activeCastCoroutine;
    private Coroutine activeMeleeCoroutine;
    private Coroutine activeLockCoroutine;

    private GameObject currentCastingVFXInstance;
    private Collider[] _aoeBuffer = new Collider[100];
    private List<CharacterRoot> _affectedCharactersBuffer = new List<CharacterRoot>(100);

    // Hashes
    private int attackHash;
    private int attackSpeedHash;
    private int attackStyleHash;
    private int castTriggerHash;
    private bool hasCastTrigger = false;

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

        audioSource = GetComponent<AudioSource>(); // Get the audio source
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

        if (targetLayers.value == 0) targetLayers = LayerMask.GetMask("Default", "Player", "Enemy");
        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            attackHash = Animator.StringToHash(defaultAttackTrigger);
            attackSpeedHash = Animator.StringToHash(attackSpeedParam);
        }
    }

    void OnDisable()
    {
        CancelCast();
        ClearInputBuffer();
        if (ActiveBeam != null) { ActiveBeam.Interrupt(); ActiveBeam = null; }
    }

    void Update()
    {
        if (ActiveBeam != null && ActiveBeam.gameObject == null) { ActiveBeam = null; }

        if (hasQueuedAction)
        {
            if (Time.time > bufferExpirationTime) ClearInputBuffer();
            else if (!IsCasting && !IsAnimationLocked && !IsOnGlobalCooldown())
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
        if (playerStats != null && playerStats.currentMana < ability.manaCost)
        {
            if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowText("No Mana!", transform.position + Vector3.up * 2, Color.blue);
            return;
        }
        if (!bypassCooldown && cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return;

        bool isBusy = IsCasting || (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) || IsAnimationLocked;
        if (isBusy)
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

    private void ClearInputBuffer()
    {
        hasQueuedAction = false;
        queuedAbility = null;
        queuedTarget = null;
    }

    private IEnumerator PerformCast(Ability ability, GameObject target, Vector3 position, float castTime, bool bypassCooldown)
    {
        IsCasting = true;
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath();

        // 1. Play Windup Animation
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(ability.telegraphAnimationTrigger)) animator.SetTrigger(ability.telegraphAnimationTrigger);
            else if (hasCastTrigger) animator.SetTrigger(castTriggerHash);
        }

        // 2. Play Windup Audio (Looping check handled by clip settings usually, or re-triggered)
        if (ability.windupSound != null && audioSource != null)
        {
            audioSource.clip = ability.windupSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // 3. Spawn Windup VFX (Respect Parenting Toggle)
        if (ability.castingVFX != null)
        {
            Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
            currentCastingVFXInstance = ObjectPooler.instance.Get(ability.castingVFX, spawnTransform.position, spawnTransform.rotation);
            if (currentCastingVFXInstance != null && ability.attachCastingVFX)
            {
                currentCastingVFXInstance.transform.SetParent(spawnTransform);
            }
        }

        OnCastStarted?.Invoke(ability.abilityName, castTime);

        try
        {
            if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StartCast(ability.abilityName, castTime);

            if (ability.telegraphDuration > 0) yield return new WaitForSeconds(ability.telegraphDuration);
            if (castTime > 0) yield return new WaitForSeconds(castTime);

            if (target == null && playerMovement != null) position = playerMovement.CurrentLookTarget;

            ExecuteAbility(ability, target, position, bypassCooldown, false);
        }
        finally
        {
            CleanupCastingVFX();

            // Stop Windup Audio
            if (audioSource != null && audioSource.clip == ability.windupSound)
            {
                audioSource.Stop();
                audioSource.loop = false;
                audioSource.clip = null;
            }

            IsCasting = false;
            activeCastCoroutine = null;
            if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StopCast();
            if (HotbarManager.instance != null && HotbarManager.instance.LockingAbility == ability) HotbarManager.instance.LockingAbility = null;
            OnCastFinished?.Invoke();
        }
    }

    private void CleanupCastingVFX()
    {
        if (currentCastingVFXInstance != null)
        {
            VFXGraphCleaner cleaner = currentCastingVFXInstance.GetComponent<VFXGraphCleaner>();
            if (cleaner != null) cleaner.StopAndFade();
            else
            {
                PooledObject pooled = currentCastingVFXInstance.GetComponent<PooledObject>();
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

        // --- AAA Audio: Play Cast Sound (Fire/Swing) ---
        if (ability.castSound != null) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);

        // --- AAA VFX: Spawn Cast VFX (Respect Parenting Toggle) ---
        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        if (ability.castVFX != null)
        {
            GameObject vfxInstance = ObjectPooler.instance.Get(ability.castVFX, spawnTransform.position, spawnTransform.rotation);
            if (vfxInstance != null && ability.attachCastVFX)
            {
                vfxInstance.transform.SetParent(spawnTransform);
            }
        }

        // --- AAA Feel: Screen Shake ---
        if (ability.screenShakeIntensity > 0)
        {
            OnCameraShakeRequest?.Invoke(ability.screenShakeIntensity, ability.screenShakeDuration);
        }

        OnPlayerAbilityUsed?.Invoke(this, ability);

        if (triggerAnimation) TriggerAttackAnimation(ability);

        if (ability.movementLockDuration > 0)
        {
            if (activeLockCoroutine != null) StopCoroutine(activeLockCoroutine);
            activeLockCoroutine = StartCoroutine(HandleMovementLock(ability.movementLockDuration));
        }

        switch (ability.abilityType)
        {
            case AbilityType.TargetedProjectile:
            case AbilityType.ForwardProjectile:
                HandleProjectile(ability, target, position);
                break;
            case AbilityType.TargetedMelee:
            case AbilityType.DirectionalMelee:
                if (activeMeleeCoroutine != null) { StopCoroutine(activeMeleeCoroutine); meleeHitbox.gameObject.SetActive(false); }
                activeMeleeCoroutine = StartCoroutine(PerformMeleeAttackWithTimers(ability));
                break;
            case AbilityType.GroundAOE:
                HandleGroundAOE(ability, position);
                break;
            case AbilityType.Self:
                HandleSelfCast(ability);
                break;
            case AbilityType.GroundPlacement:
                HandleGroundPlacement(ability, position);
                break;
            case AbilityType.ChanneledBeam:
                HandleChanneledBeam(ability, target);
                break;
            case AbilityType.Charge:
                if (playerMovement != null) playerMovement.InitiateCharge(target, ability);
                break;
            case AbilityType.Leap:
                if (movementHandler != null) movementHandler.ExecuteLeap(position, () => { HandleGroundAOE(ability, position); });
                break;
            case AbilityType.Teleport:
                if (movementHandler != null) movementHandler.ExecuteTeleport(position);
                break;
        }
    }

    private IEnumerator HandleMovementLock(float baseDuration)
    {
        IsAnimationLocked = true;
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.velocity = Vector3.zero;
        }

        float speedMultiplier = 1f;
        if (playerStats != null && playerStats.secondaryStats.attackSpeed > 0)
        {
            speedMultiplier = playerStats.secondaryStats.attackSpeed;
        }

        yield return new WaitForSeconds(baseDuration / speedMultiplier);

        IsAnimationLocked = false;
        activeLockCoroutine = null;
    }

    private IEnumerator PerformMeleeAttackWithTimers(Ability ability)
    {
        meleeHitbox.Setup(ability, this.gameObject);
        BoxCollider collider = meleeHitbox.GetComponent<BoxCollider>();
        collider.size = ability.attackBoxSize;
        collider.center = ability.attackBoxCenter;

        float speedMultiplier = 1f;
        if (playerStats != null && playerStats.secondaryStats.attackSpeed > 0)
        {
            speedMultiplier = playerStats.secondaryStats.attackSpeed;
        }

        yield return new WaitForSeconds(ability.hitboxOpenDelay / speedMultiplier);

        meleeHitbox.gameObject.SetActive(true);

        float duration = ability.hitboxCloseDelay - ability.hitboxOpenDelay;
        if (duration > 0)
        {
            yield return new WaitForSeconds(duration / speedMultiplier);
        }

        meleeHitbox.gameObject.SetActive(false);
        activeMeleeCoroutine = null;
    }

    private void TriggerAttackAnimation(Ability ability)
    {
        if (animator != null)
        {
            animator.ResetTrigger(attackHash);

            float animSpeed = 1.0f;
            if (playerStats != null) { animSpeed = playerStats.secondaryStats.attackSpeed; }
            animator.SetFloat(attackSpeedHash, animSpeed);

            if (!string.IsNullOrEmpty(ability.overrideTriggerName))
            {
                animator.SetTrigger(ability.overrideTriggerName);
            }
            else
            {
                animator.SetInteger(attackStyleHash, ability.attackStyleIndex);
                animator.SetTrigger(attackHash);
            }
        }
    }

    public void CancelCast()
    {
        if (activeCastCoroutine != null) { StopCoroutine(activeCastCoroutine); activeCastCoroutine = null; }

        // Clean up Audio on Cancel
        if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }

        IsCasting = false;
        CleanupCastingVFX();
        ClearInputBuffer();

        IsAnimationLocked = false;
        if (activeLockCoroutine != null) { StopCoroutine(activeLockCoroutine); activeLockCoroutine = null; }
        if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StopCast();
        if (HotbarManager.instance != null) HotbarManager.instance.LockingAbility = null;
        if (ActiveBeam != null)
        {
            ActiveBeam.Interrupt();
            ActiveBeam = null;
        }
        if (activeMeleeCoroutine != null)
        {
            StopCoroutine(activeMeleeCoroutine);
            activeMeleeCoroutine = null;
            if (meleeHitbox != null) meleeHitbox.gameObject.SetActive(false);
        }
    }

    public void PayCostAndStartCooldown(Ability ability, bool bypassCooldown = false)
    {
        if (playerStats != null) { playerStats.SpendMana(ability.manaCost); }
        if (!bypassCooldown)
        {
            if (ability.abilityType != AbilityType.ChanneledBeam)
            {
                float baseCooldown = ability.cooldown;
                if (playerMovement != null && ability == playerMovement.defaultAttackAbility) { baseCooldown = GetCurrentWeaponSpeed(); }
                float finalCooldown = baseCooldown / (playerStats != null ? playerStats.secondaryStats.attackSpeed : 1f);
                if (playerStats != null) { finalCooldown *= playerStats.secondaryStats.cooldownReduction; }
                cooldowns[ability] = Time.time + finalCooldown;
            }
            if (ability.triggersGlobalCooldown)
            {
                float effectiveGcd = globalCooldownDuration / (playerStats != null ? playerStats.secondaryStats.attackSpeed : 1f);
                globalCooldownTimer = Time.time + Mathf.Max(minGlobalCooldown, effectiveGcd);
            }
        }
    }

    private void HandleProjectile(Ability ability, GameObject target, Vector3 targetPos)
    {
        if (ability.playerProjectilePrefab == null) return;

        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        Vector3 spawnPos = spawnTransform.position;
        Quaternion spawnRot = spawnTransform.rotation;

        if (target != null)
        {
            Vector3 targetCenter = target.transform.position;
            Collider targetCollider = target.GetComponent<Collider>();
            if (targetCollider == null) targetCollider = target.GetComponentInChildren<Collider>();
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
            Collider projectileCollider = projectileGO.GetComponent<Collider>();
            if (projectileCollider != null)
            {
                Collider[] casterColliders = GetComponentInParent<CharacterRoot>().GetComponentsInChildren<Collider>();
                foreach (Collider c in casterColliders) { Physics.IgnoreCollision(projectileCollider, c); }
            }

            if (projectileGO.TryGetComponent<Projectile>(out var projectile))
            {
                projectile.Initialize(ability, this.gameObject, GetComponentInParent<CharacterRoot>().gameObject.layer);
            }
        }
    }

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        // Spawns HitVFX at location (usually World Space)
        if (ability.hitVFX != null) ObjectPooler.instance.Get(ability.hitVFX, position, Quaternion.identity);

        // Play Impact Sound
        if (ability.impactSound != null) AudioSource.PlayClipAtPoint(ability.impactSound, position);

        int hitCount = Physics.OverlapSphereNonAlloc(position, ability.aoeRadius, _aoeBuffer, targetLayers);
        _affectedCharactersBuffer.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _aoeBuffer[i];
            CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();
            if (hitCharacter == null || _affectedCharactersBuffer.Contains(hitCharacter)) { continue; }
            _affectedCharactersBuffer.Add(hitCharacter);

            CharacterRoot casterRoot = this.GetComponentInParent<CharacterRoot>();
            if (casterRoot == null) return;

            int casterLayer = casterRoot.gameObject.layer;
            int targetLayer = hitCharacter.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;
            var effectsToApply = isAlly ? ability.friendlyEffects : ability.hostileEffects;
            foreach (var effect in effectsToApply) { effect.Apply(casterRoot.gameObject, hitCharacter.gameObject); }
        }
    }

    private void HandleSelfCast(Ability ability)
    {
        CharacterRoot casterRoot = this.GetComponentInParent<CharacterRoot>();
        if (casterRoot == null) return;

        // Play Impact Sound at self
        if (ability.impactSound != null) AudioSource.PlayClipAtPoint(ability.impactSound, transform.position);

        if (ability.aoeRadius <= 0)
        {
            foreach (var effect in ability.friendlyEffects) { effect.Apply(casterRoot.gameObject, casterRoot.gameObject); }
        }
        else
        {
            int hitCount = Physics.OverlapSphereNonAlloc(casterRoot.transform.position, ability.aoeRadius, _aoeBuffer, targetLayers);
            _affectedCharactersBuffer.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _aoeBuffer[i];
                CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();
                if (hitCharacter == null || _affectedCharactersBuffer.Contains(hitCharacter)) { continue; }

                if (hitCharacter.gameObject.layer == casterRoot.gameObject.layer)
                {
                    _affectedCharactersBuffer.Add(hitCharacter);
                    foreach (var effect in ability.friendlyEffects) { effect.Apply(casterRoot.gameObject, hitCharacter.gameObject); }
                }
            }
        }
    }

    private void HandleChanneledBeam(Ability ability, GameObject target)
    {
        GameObject prefabToSpawn = ability.playerProjectilePrefab != null ? ability.playerProjectilePrefab : ability.enemyProjectilePrefab;

        if (prefabToSpawn != null)
        {
            GameObject beamObject = Instantiate(prefabToSpawn, transform.position, transform.rotation, transform);
            if (beamObject.TryGetComponent<ChanneledBeamController>(out var beam))
            {
                beam.Initialize(ability, this.gameObject, target, projectileSpawnPoint);
                ActiveBeam = beam;
                if (HotbarManager.instance != null)
                {
                    if (ability.locksPlayerActivity) HotbarManager.instance.LockingAbility = ability;
                    HotbarManager.instance.SetActiveBeam(beam);
                }
            }
        }
    }

    private void HandleGroundPlacement(Ability ability, Vector3 position)
    {
        if (ability.placementPrefab != null)
        {
            GameObject trapObject = Instantiate(ability.placementPrefab, position, Quaternion.identity);
            PlaceableTrap trap = trapObject.GetComponent<PlaceableTrap>();
            if (trap != null)
            {
                CharacterRoot casterRoot = this.GetComponentInParent<CharacterRoot>();
                if (casterRoot != null) { trap.owner = casterRoot.gameObject; }
            }
        }
    }

    public bool GetCooldownStatus(Ability ability, out float remaining) { remaining = 0f; if (cooldowns.TryGetValue(ability, out float endTime)) { if (Time.time < endTime) { remaining = endTime - Time.time; return true; } } return false; }
    public bool CanUseAbility(Ability ability, GameObject target) { if (ability == null || IsCasting) return false; if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false; if (ability.requiresWeaponType && !IsCorrectWeaponEquipped(ability.requiredWeaponCategories)) return false; if ((ability.abilityType == AbilityType.TargetedMelee || ability.abilityType == AbilityType.TargetedProjectile || ability.abilityType == AbilityType.Charge) && target == null) return false; if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false; if (playerStats != null && playerStats.currentMana < ability.manaCost) return false; return true; }
    private float GetCurrentWeaponSpeed() { if (playerEquipment == null) return 2.0f; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem) && rightHandItem?.itemData.stats is ItemWeaponStats rightWeapon) return rightWeapon.baseAttackTime; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem) && leftHandItem?.itemData.stats is ItemWeaponStats leftWeapon) return leftWeapon.baseAttackTime; return 2.0f; }
    public bool IsCorrectWeaponEquipped(List<ItemWeaponStats.WeaponCategory> categories) { if (playerEquipment == null) return false; if (categories == null || categories.Count == 0) return false; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem) && rightHandItem?.itemData?.stats is ItemWeaponStats rightWeapon && categories.Contains(rightWeapon.weaponCategory)) return true; if (playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem) && leftHandItem?.itemData?.stats is ItemWeaponStats leftWeapon && categories.Contains(leftWeapon.weaponCategory)) return true; return false; }
}