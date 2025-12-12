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

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.5f;

    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;

    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private IMovementHandler movementHandler;
    private Coroutine activeCastCoroutine;
    private EnemyAI enemyAI;
    private float globalCooldownTimer = 0f;
    private Animator animator;

    // Physics Buffer
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
    }

    void OnDisable()
    {
        if (activeCastCoroutine != null)
        {
            StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = null;
        }
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
            if (ability.telegraphDuration > 0)
            {
                if (animator != null && !string.IsNullOrEmpty(ability.telegraphAnimationTrigger))
                    animator.SetTrigger(ability.telegraphAnimationTrigger);
                yield return new WaitForSeconds(ability.telegraphDuration);
            }

            OnCastStarted?.Invoke(ability.abilityName, ability.castTime);
            if (ability.castTime > 0) yield return new WaitForSeconds(ability.castTime);

            ExecuteAbility(ability, target, position, bypassCooldown);
        }
        finally
        {
            if (ability.abilityType != AbilityType.TargetedMelee && ability.abilityType != AbilityType.DirectionalMelee)
            {
                IsCasting = false;
                activeCastCoroutine = null;
                OnCastFinished?.Invoke();
            }
        }
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false)
    {
        PayCostAndStartCooldown(ability, bypassCooldown);
        if (ability.castSound != null) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);
        if (ability.castVFX != null) ObjectPooler.instance.Get(ability.castVFX, transform.position, transform.rotation);

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

    private IEnumerator PerformSmartMeleeAttack(Ability ability)
    {
        IsCasting = true;

        // 1. Windup
        float windup = (ability.hitboxOpenDelay > 0) ? ability.hitboxOpenDelay : 0.1f;
        yield return new WaitForSeconds(windup);

        // 2. Hit Check
        CheckHit(ability);

        // 3. Duration
        float remainingDuration = ability.hitboxCloseDelay - ability.hitboxOpenDelay;
        if (remainingDuration <= 0.05f) remainingDuration = 0.25f;

        yield return new WaitForSeconds(remainingDuration);

        // 4. Reset
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
                // --- FIX STARTS HERE ---
                CharacterRoot myRoot = GetComponentInParent<CharacterRoot>();

                // Try to get root, but allow null (for buildings/domes)
                CharacterRoot targetRoot = hit.GetComponentInParent<CharacterRoot>();

                if (myRoot != null)
                {
                    // Determine target's layer properly
                    int targetLayer = (targetRoot != null) ? targetRoot.gameObject.layer : hit.gameObject.layer;

                    // Allow hitting anything on a different layer
                    if (myRoot.gameObject.layer != targetLayer)
                    {
                        // Use the Root if we have it, otherwise use the hit object itself as the "target"
                        GameObject targetObj = (targetRoot != null) ? targetRoot.gameObject : hit.gameObject;

                        foreach (var effect in ability.hostileEffects)
                        {
                            effect.Apply(myRoot.gameObject, targetObj);
                        }
                    }
                }
                // --- FIX ENDS HERE ---
            }
        }
    }

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

    // --- Helpers ---
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

    private void HandleProjectile(Ability ability, GameObject target)
    {
        GameObject prefabToSpawn = ability.enemyProjectilePrefab != null ? ability.enemyProjectilePrefab : ability.playerProjectilePrefab;
        if (prefabToSpawn == null) return;

        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        Vector3 spawnPos = spawnTransform.position;
        Quaternion spawnRot = spawnTransform.rotation;

        if (target != null)
        {
            Vector3 targetPosition = target.transform.position;
            Vector3 direction = targetPosition - spawnPos;
            direction.y = 0;
            if (direction != Vector3.zero)
                spawnRot = Quaternion.LookRotation(direction);
        }

        GameObject projectileGO = ObjectPooler.instance.Get(prefabToSpawn, spawnPos, spawnRot);
        if (projectileGO == null) return;

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
    }

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null) ObjectPooler.instance.Get(ability.hitVFX, position, Quaternion.identity);
        Collider[] hits = Physics.OverlapSphere(position, ability.aoeRadius);
        List<CharacterRoot> affectedCharacters = new List<CharacterRoot>();
        foreach (var hit in hits)
        {
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
}