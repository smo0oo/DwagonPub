using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class PlayerAbilityHolder : MonoBehaviour
{
    // Restored Events for external listeners (EnemyAI, etc)
    public static event Action<PlayerAbilityHolder, Ability> OnPlayerAbilityUsed;
    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;

    [Header("Component References")]
    [Tooltip("Assign the transform where projectiles spawn (e.g. Wand Tip, Hand). Defaults to self if null.")]
    public Transform projectileSpawnPoint;

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.5f;

    // --- State ---
    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;
    public bool IsAnimationLocked { get; private set; } = false;
    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;

    // --- Internal Data ---
    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private float globalCooldownTimer = 0f;
    private Collider[] hitBuffer = new Collider[20];

    // --- Components ---
    private IMovementHandler movementHandler;
    private PlayerMovement playerMovement;
    private Animator animator;
    private CharacterRoot myRoot;
    private PlayerEquipment playerEquipment;
    private PlayerStats playerStats;
    private Coroutine activeCastCoroutine;

    // --- Optimization: Cached Hashes ---
    private static readonly int CastTriggerHash = Animator.StringToHash("CastTrigger");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

    // --- Debug ---
    private Vector3 debugBoxCenter, debugBoxSize;
    private Quaternion debugBoxRotation;
    private float debugDisplayTime;

    void Awake()
    {
        movementHandler = GetComponentInParent<IMovementHandler>();
        playerMovement = GetComponentInParent<PlayerMovement>();
        animator = GetComponentInParent<Animator>();
        myRoot = GetComponentInParent<CharacterRoot>();

        if (myRoot != null)
        {
            playerEquipment = myRoot.PlayerEquipment;
            playerStats = myRoot.PlayerStats;
        }

        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
    }

    void OnDisable()
    {
        CancelCast();
    }

    public void CancelCast()
    {
        if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
        activeCastCoroutine = null;
        IsCasting = false;
        IsAnimationLocked = false;

        // --- FIX: Stop UI when cast is cancelled ---
        if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StopCast();
        if (HotbarManager.instance != null) HotbarManager.instance.LockingAbility = null;
        // -------------------------------------------

        if (ActiveBeam != null)
        {
            ActiveBeam.Interrupt();
            ActiveBeam = null;
        }
    }

    public void UseAbility(Ability ability, Vector3 targetPosition)
    {
        if (!CanUseAbility(ability, null)) return;

        if (ability.castTime > 0 || ability.telegraphDuration > 0)
        {
            CancelCast();
            activeCastCoroutine = StartCoroutine(PerformCast(ability, null, targetPosition, false));
        }
        else
        {
            ExecuteAbility(ability, null, targetPosition, false);
        }
    }

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown = false)
    {
        if (!CanUseAbility(ability, target)) return;

        Vector3 position = transform.position;
        if (target != null)
        {
            position = target.transform.position;
        }
        else if (playerMovement != null)
        {
            position = playerMovement.CurrentLookTarget;
        }

        if (ability.castTime > 0 || ability.telegraphDuration > 0)
        {
            CancelCast();
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

        // 1. Telegraph
        if (ability.telegraphDuration > 0)
        {
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(ability.telegraphAnimationTrigger))
                    animator.SetTrigger(ability.telegraphAnimationTrigger);
                else
                    animator.SetTrigger(CastTriggerHash);
            }
            yield return new WaitForSeconds(ability.telegraphDuration);
        }

        // 2. Cast Timer
        OnCastStarted?.Invoke(ability.abilityName, ability.castTime);

        // --- FIX: Restore Direct UI Call ---
        if (CastingBarUIManager.instance != null)
            CastingBarUIManager.instance.StartCast(ability.abilityName, ability.castTime);
        // -----------------------------------

        if (ability.castTime > 0) yield return new WaitForSeconds(ability.castTime);

        // 3. Execute
        // Re-aim at mouse cursor just before firing for maximum precision
        if (target == null && playerMovement != null) position = playerMovement.CurrentLookTarget;

        ExecuteAbility(ability, target, position, bypassCooldown);

        // 4. Cleanup
        if (ability.abilityType != AbilityType.TargetedMelee && ability.abilityType != AbilityType.DirectionalMelee)
        {
            IsCasting = false;
            IsAnimationLocked = false;
            activeCastCoroutine = null;
            if (CastingBarUIManager.instance != null) CastingBarUIManager.instance.StopCast(); // Stop UI
            OnCastFinished?.Invoke();
        }
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false)
    {
        PayCostAndStartCooldown(ability, bypassCooldown);
        OnPlayerAbilityUsed?.Invoke(this, ability);

        if (ability.castSound != null) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);
        if (ability.castVFX != null) ObjectPooler.instance.Get(ability.castVFX, transform.position, transform.rotation);

        switch (ability.abilityType)
        {
            case AbilityType.TargetedMelee:
            case AbilityType.DirectionalMelee:
                CancelCast();
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
                if (movementHandler != null)
                    movementHandler.ExecuteLeap(position, () => HandleGroundAOE(ability, position));
                break;

            case AbilityType.Charge:
                if (movementHandler is PlayerMovement pm)
                    pm.InitiateCharge(target, ability);
                break;

            case AbilityType.Teleport:
                if (movementHandler != null) movementHandler.ExecuteTeleport(position);
                break;

            case AbilityType.GroundPlacement:
                HandleGroundPlacement(ability, position);
                break;
        }
    }

    private IEnumerator PerformSmartMeleeAttack(Ability ability)
    {
        IsCasting = true;
        IsAnimationLocked = true;

        float windup = Mathf.Max(0.1f, ability.hitboxOpenDelay);
        yield return new WaitForSeconds(windup);

        CheckHit(ability);

        float recovery = Mathf.Max(0.1f, ability.hitboxCloseDelay - ability.hitboxOpenDelay);
        yield return new WaitForSeconds(recovery);

        IsCasting = false;
        IsAnimationLocked = false;
        activeCastCoroutine = null;
        OnCastFinished?.Invoke();
    }

    private void CheckHit(Ability ability)
    {
        float boxLength = ability.range + 0.5f;
        Vector3 size = ability.attackBoxSize;
        Vector3 halfExtents = new Vector3((size.x > 0 ? size.x : 2f) / 2f, (size.y > 0 ? size.y : 2f) / 2f, boxLength / 2f);
        Vector3 center = transform.position + (transform.forward * (boxLength / 2f)) + (Vector3.up * 1f);

        debugBoxCenter = center; debugBoxSize = halfExtents * 2; debugBoxRotation = transform.rotation; debugDisplayTime = 0.5f;

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, hitBuffer, transform.rotation);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit.transform.IsChildOf(transform.root)) continue;

            Health targetHealth = hit.GetComponentInChildren<Health>() ?? hit.GetComponentInParent<Health>();

            if (targetHealth != null && myRoot != null)
            {
                CharacterRoot targetRoot = targetHealth.GetComponentInParent<CharacterRoot>();
                int targetLayer = (targetRoot != null) ? targetRoot.gameObject.layer : hit.gameObject.layer;

                if (myRoot.gameObject.layer != targetLayer)
                {
                    GameObject applyTarget = (targetRoot != null) ? targetRoot.gameObject : hit.gameObject;
                    foreach (var effect in ability.hostileEffects) effect.Apply(myRoot.gameObject, applyTarget);
                }
            }
        }
    }

    private void HandleProjectile(Ability ability, GameObject target)
    {
        GameObject prefab = ability.playerProjectilePrefab ?? ability.enemyProjectilePrefab;
        if (prefab == null) return;

        Vector3 spawnPos = projectileSpawnPoint.position;
        Quaternion rotation = projectileSpawnPoint.rotation;

        if (target != null)
        {
            Vector3 dir = (target.transform.position - spawnPos).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) rotation = Quaternion.LookRotation(dir);
        }
        else if (playerMovement != null)
        {
            Vector3 mousePos = playerMovement.CurrentLookTarget;
            mousePos.y = spawnPos.y;
            Vector3 dir = (mousePos - spawnPos).normalized;
            if (dir != Vector3.zero) rotation = Quaternion.LookRotation(dir);
        }

        GameObject projGO = ObjectPooler.instance.Get(prefab, spawnPos, rotation);

        if (projGO.TryGetComponent<Projectile>(out var proj))
        {
            // --- FIX: Reverted Layer Name to match your project settings ---
            // If "FriendlyRanged" is missing, use LayerMask.NameToLayer("Default")
            int layerID = LayerMask.NameToLayer("FriendlyRanged");
            if (layerID == -1) layerID = 0; // Fallback to Default to prevent crash
            proj.gameObject.layer = layerID;
            // ---------------------------------------------------------------

            proj.Initialize(ability, this.gameObject, myRoot != null ? myRoot.gameObject.layer : gameObject.layer);
        }
    }

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null) ObjectPooler.instance.Get(ability.hitVFX, position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(position, ability.aoeRadius);
        foreach (var hit in hits)
        {
            CharacterRoot hitRoot = hit.GetComponentInParent<CharacterRoot>();
            if (hitRoot == null || hitRoot == myRoot) continue;

            bool isAlly = (hitRoot.gameObject.layer == myRoot.gameObject.layer);
            var effects = isAlly ? ability.friendlyEffects : ability.hostileEffects;

            foreach (var effect in effects) effect.Apply(myRoot.gameObject, hitRoot.gameObject);
        }
    }

    private void HandleSelfCast(Ability ability)
    {
        foreach (var effect in ability.friendlyEffects) effect.Apply(gameObject, gameObject);
    }

    private void HandleGroundPlacement(Ability ability, Vector3 position)
    {
        if (ability.placementPrefab != null)
        {
            GameObject trapObject = Instantiate(ability.placementPrefab, position, Quaternion.identity);
            PlaceableTrap trap = trapObject.GetComponent<PlaceableTrap>();
            if (trap != null && myRoot != null) trap.owner = myRoot.gameObject;
        }
    }

    private void HandleChanneledBeam(Ability ability, GameObject target)
    {
        GameObject prefab = ability.playerProjectilePrefab ?? ability.enemyProjectilePrefab;
        if (prefab != null)
        {
            var beamObj = Instantiate(prefab, transform.position, transform.rotation);
            if (beamObj.TryGetComponent<ChanneledBeamController>(out var beam))
            {
                beam.Initialize(ability, gameObject, target);
                ActiveBeam = beam;

                // --- FIX: Notify Hotbar Manager for Beam UI ---
                if (HotbarManager.instance != null)
                {
                    if (ability.locksPlayerActivity) HotbarManager.instance.LockingAbility = ability;
                    HotbarManager.instance.SetActiveBeam(beam);
                }
                // ----------------------------------------------
            }
        }
    }

    // --- Helpers ---

    public void PayCostAndStartCooldown(Ability ability, bool bypass = false)
    {
        if (playerStats != null) playerStats.SpendMana(ability.manaCost);

        if (bypass) return;
        cooldowns[ability] = Time.time + ability.cooldown;
        if (ability.triggersGlobalCooldown) globalCooldownTimer = Time.time + globalCooldownDuration;
    }

    public bool CanUseAbility(Ability ability, GameObject target)
    {
        if (ability == null || IsCasting || IsAnimationLocked) return false;
        if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false;

        if (ability.requiresWeaponType && !IsCorrectWeaponEquipped(ability.requiredWeaponCategories)) return false;

        if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false;

        if (playerStats != null && playerStats.currentMana < ability.manaCost) return false;

        if (target != null)
        {
            float rangeSqr = ability.range * ability.range;
            if ((target.transform.position - transform.position).sqrMagnitude > rangeSqr) return false;
        }
        return true;
    }

    public bool GetCooldownStatus(Ability ability, out float remaining)
    {
        remaining = 0f;
        if (cooldowns.TryGetValue(ability, out float endTime))
        {
            if (Time.time < endTime)
            {
                remaining = endTime - Time.time;
                return true;
            }
        }
        return false;
    }

    public bool IsCorrectWeaponEquipped(List<ItemWeaponStats.WeaponCategory> categories)
    {
        if (playerEquipment == null) return false;
        if (categories == null || categories.Count == 0) return false;

        playerEquipment.equippedItems.TryGetValue(EquipmentType.RightHand, out var rightHandItem);
        playerEquipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out var leftHandItem);

        if (rightHandItem?.itemData?.stats is ItemWeaponStats rightWeapon && categories.Contains(rightWeapon.weaponCategory)) return true;
        if (leftHandItem?.itemData?.stats is ItemWeaponStats leftWeapon && categories.Contains(leftWeapon.weaponCategory)) return true;

        return false;
    }

    private void OnDrawGizmos()
    {
        if (debugDisplayTime > 0)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.matrix = Matrix4x4.TRS(debugBoxCenter, debugBoxRotation, debugBoxSize);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            debugDisplayTime -= Time.deltaTime;
        }
    }
}