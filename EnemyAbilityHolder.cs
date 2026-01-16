using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class EnemyAbilityHolder : MonoBehaviour
{
    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;

    [Header("Component References")]
    [Tooltip("Assign the transform where projectiles spawn (e.g. Wand Tip, Mouth). Defaults to self if null.")]
    public Transform projectileSpawnPoint;

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.5f;

    // --- State ---
    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;
    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;

    // --- Internal Data ---
    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private float globalCooldownTimer = 0f;
    private Collider[] hitBuffer = new Collider[20]; // Optimization: Reusable Physics Buffer

    // --- Components ---
    private IMovementHandler movementHandler;
    private EnemyAI enemyAI;
    private Animator animator;
    private CharacterRoot myRoot;
    private Coroutine activeCastCoroutine;

    // --- Optimization: Cached Animator Hashes ---
    // These are 10x faster for Unity to read than Strings
    private static readonly int CastTriggerHash = Animator.StringToHash("CastTrigger");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

    // --- Debug ---
    private Vector3 debugBoxCenter, debugBoxSize;
    private Quaternion debugBoxRotation;
    private float debugDisplayTime;

    void Awake()
    {
        movementHandler = GetComponentInParent<IMovementHandler>();
        enemyAI = GetComponentInParent<EnemyAI>();
        animator = GetComponentInParent<Animator>();
        myRoot = GetComponentInParent<CharacterRoot>();

        // Fail-safe if spawn point is missing
        if (projectileSpawnPoint == null) projectileSpawnPoint = transform;
    }

    void OnDisable()
    {
        StopCasting();
    }

    private void StopCasting()
    {
        if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
        activeCastCoroutine = null;
        IsCasting = false;

        if (ActiveBeam != null)
        {
            ActiveBeam.Interrupt();
            ActiveBeam = null;
        }
    }

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown = false)
    {
        if (!CanUseAbility(ability, target)) return;

        // Determine target position (or self/forward if null)
        Vector3 position = (target != null) ? target.transform.position : transform.position;

        // Decide: Coroutine (Telegraph/CastTime) vs Instant
        if (ability.castTime > 0 || ability.telegraphDuration > 0)
        {
            StopCasting();
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

        // 1. Telegraph Phase
        if (ability.telegraphDuration > 0)
        {
            if (animator != null)
            {
                // Optimization: Use Hash if generic, or String if specific override exists
                if (!string.IsNullOrEmpty(ability.telegraphAnimationTrigger))
                    animator.SetTrigger(ability.telegraphAnimationTrigger);
                else
                    animator.SetTrigger(CastTriggerHash);
            }
            yield return new WaitForSeconds(ability.telegraphDuration);
        }

        // 2. Cast Timer Phase
        OnCastStarted?.Invoke(ability.abilityName, ability.castTime);
        if (ability.castTime > 0) yield return new WaitForSeconds(ability.castTime);

        // 3. Execution
        ExecuteAbility(ability, target, position, bypassCooldown);

        // 4. Cleanup (Unless it's melee which manages its own state)
        if (ability.abilityType != AbilityType.TargetedMelee && ability.abilityType != AbilityType.DirectionalMelee)
        {
            IsCasting = false;
            activeCastCoroutine = null;
            OnCastFinished?.Invoke();
        }
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false)
    {
        PayCostAndStartCooldown(ability, bypassCooldown);

        // Audio/VFX
        if (ability.castSound != null) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);
        if (ability.castVFX != null) ObjectPooler.instance.Get(ability.castVFX, transform.position, transform.rotation);

        switch (ability.abilityType)
        {
            case AbilityType.TargetedMelee:
            case AbilityType.DirectionalMelee:
                StopCasting(); // Ensure we don't overlap
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
                if (enemyAI != null && target != null)
                    enemyAI.ExecuteCharge(target, ability);
                break;

            case AbilityType.Teleport:
                HandleTeleport(enemyAI?.currentTarget);
                break;
        }
    }

    private IEnumerator PerformSmartMeleeAttack(Ability ability)
    {
        IsCasting = true;

        // Windup
        float windup = Mathf.Max(0.1f, ability.hitboxOpenDelay);
        yield return new WaitForSeconds(windup);

        // Strike
        CheckHit(ability);

        // Recovery
        float recovery = Mathf.Max(0.25f, ability.hitboxCloseDelay - ability.hitboxOpenDelay);
        yield return new WaitForSeconds(recovery);

        IsCasting = false;
        activeCastCoroutine = null;
        OnCastFinished?.Invoke();
    }

    private void CheckHit(Ability ability)
    {
        float boxLength = ability.range + 0.5f;
        Vector3 size = ability.attackBoxSize;
        Vector3 halfExtents = new Vector3((size.x > 0 ? size.x : 2f) / 2f, (size.y > 0 ? size.y : 2f) / 2f, boxLength / 2f);
        Vector3 center = transform.position + (transform.forward * (boxLength / 2f)) + (Vector3.up * 1f);

        // Debug visualization
        debugBoxCenter = center; debugBoxSize = halfExtents * 2; debugBoxRotation = transform.rotation; debugDisplayTime = 0.5f;

        // Optimization: NonAlloc avoids Garbage Collection
        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, hitBuffer, transform.rotation);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            // Skip self and own children
            if (hit.transform.IsChildOf(transform.root)) continue;

            Health targetHealth = hit.GetComponentInChildren<Health>(); // Check children for parts
            if (targetHealth == null) targetHealth = hit.GetComponentInParent<Health>(); // Check parent for root

            if (targetHealth != null && myRoot != null)
            {
                // Root check handles complex hierarchies (e.g. Shield -> Player)
                CharacterRoot targetRoot = targetHealth.GetComponentInParent<CharacterRoot>();
                int targetLayer = (targetRoot != null) ? targetRoot.gameObject.layer : hit.gameObject.layer;

                // Faction Check (Layer based)
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
        GameObject prefab = ability.enemyProjectilePrefab ?? ability.playerProjectilePrefab;
        if (prefab == null) return;

        // Calculate Rotation
        Quaternion rotation = projectileSpawnPoint.rotation;
        if (target != null)
        {
            Vector3 dir = (target.transform.position - projectileSpawnPoint.position).normalized;
            dir.y = 0; // Keep projectiles level typically
            if (dir != Vector3.zero) rotation = Quaternion.LookRotation(dir);
        }

        GameObject projGO = ObjectPooler.instance.Get(prefab, projectileSpawnPoint.position, rotation);

        // Setup Layers & Collisions
        if (projGO.TryGetComponent<Projectile>(out var proj))
        {
            proj.gameObject.layer = LayerMask.NameToLayer("HostileRanged");
            // Pass the Root's layer so the projectile knows who owns it (Faction logic)
            proj.Initialize(ability, this.gameObject, myRoot != null ? myRoot.gameObject.layer : gameObject.layer);
        }
    }

    private void HandleTeleport(Transform target)
    {
        if (movementHandler == null || target == null) return;

        // Teleport 15 units AWAY from target (Kiting)
        Vector3 dirAway = (transform.position - target.position).normalized;
        Vector3 dest = transform.position + dirAway * 15f;

        if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            movementHandler.ExecuteTeleport(hit.position);
        }
    }

    // --- Helpers ---
    private void PayCostAndStartCooldown(Ability ability, bool bypass)
    {
        if (bypass) return;
        cooldowns[ability] = Time.time + ability.cooldown;
        if (ability.triggersGlobalCooldown) globalCooldownTimer = Time.time + globalCooldownDuration;
    }

    public bool CanUseAbility(Ability ability, GameObject target)
    {
        if (ability == null || IsCasting) return false;
        if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false;
        if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false;

        // Range Check (Squared optimization)
        if (target != null)
        {
            float rangeSqr = ability.range * ability.range;
            if ((target.transform.position - transform.position).sqrMagnitude > rangeSqr) return false;
        }
        return true;
    }

    // Pass-throughs for other ability types
    private void HandleGroundAOE(Ability a, Vector3 p) => ApplyAreaEffects(a, p);
    private void HandleSelfCast(Ability a) => ApplyEffects(a.friendlyEffects, gameObject); // Self is always friendly

    private void ApplyAreaEffects(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null) ObjectPooler.instance.Get(ability.hitVFX, position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(position, ability.aoeRadius);
        foreach (var hit in hits)
        {
            CharacterRoot hitRoot = hit.GetComponentInParent<CharacterRoot>();
            if (hitRoot == null || hitRoot == myRoot) continue; // Skip self

            bool isAlly = (hitRoot.gameObject.layer == myRoot.gameObject.layer);
            var effects = isAlly ? ability.friendlyEffects : ability.hostileEffects;

            foreach (var effect in effects) effect.Apply(myRoot.gameObject, hitRoot.gameObject);
        }
    }

    private void ApplyEffects(List<IAbilityEffect> effects, GameObject target)
    {
        foreach (var e in effects) e.Apply(myRoot != null ? myRoot.gameObject : gameObject, target);
    }

    private void HandleChanneledBeam(Ability ability, GameObject target)
    {
        GameObject prefab = ability.enemyProjectilePrefab ?? ability.playerProjectilePrefab;
        if (prefab != null)
        {
            var beamObj = Instantiate(prefab, transform.position, transform.rotation);
            if (beamObj.TryGetComponent<ChanneledBeamController>(out var beam))
            {
                beam.Initialize(ability, gameObject, target);
                ActiveBeam = beam;
            }
        }
    }

    // Debug Gizmos
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