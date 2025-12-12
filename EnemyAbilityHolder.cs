using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class EnemyAbilityHolder : MonoBehaviour
{
    public event Action<string, float> OnCastStarted;
    public event Action OnCastFinished;
    public event Action OnCastCancelled;

    [Header("Component References")]
    public Transform projectileSpawnPoint;

    [Header("Global Cooldown")]
    public float globalCooldownDuration = 1.5f;

    public ChanneledBeamController ActiveBeam { get; private set; }
    public bool IsCasting { get; private set; } = false;

    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();
    private MeleeHitbox meleeHitbox;
    private IMovementHandler movementHandler;
    private Coroutine activeCastCoroutine;
    private EnemyAI enemyAI;
    private float globalCooldownTimer = 0f;
    private Animator animator;

    public bool IsOnGlobalCooldown() => Time.time < globalCooldownTimer;

    void Awake()
    {
        meleeHitbox = GetComponentInChildren<MeleeHitbox>(true);
        movementHandler = GetComponentInParent<IMovementHandler>();
        enemyAI = GetComponentInParent<EnemyAI>();
        animator = GetComponentInParent<Animator>();
    }

    // --- FIX: Stop all logic immediately if the enemy is disabled/killed ---
    void OnDisable()
    {
        // 1. Stop the cast timer immediately
        if (activeCastCoroutine != null)
        {
            StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = null;
        }

        // 2. Reset the flag so the AI doesn't think it's still busy if revived/pooled later
        IsCasting = false;

        // 3. If a beam was active, cut it off
        if (ActiveBeam != null)
        {
            ActiveBeam.Interrupt();
            ActiveBeam = null;
        }

        // 4. If a melee attack was queued/active, kill it
        if (meleeHitbox != null)
        {
            meleeHitbox.gameObject.SetActive(false);
        }
    }
    // --- END FIX ---

    public void UseAbility(Ability ability, GameObject target)
    {
        UseAbility(ability, target, false);
    }

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown)
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
                if (animator != null && !string.IsNullOrEmpty(ability.telegraphAnimationTrigger)) { animator.SetTrigger(ability.telegraphAnimationTrigger); }
                yield return new WaitForSeconds(ability.telegraphDuration);
            }
            OnCastStarted?.Invoke(ability.abilityName, ability.castTime);
            yield return new WaitForSeconds(ability.castTime);
            ExecuteAbility(ability, target, position, bypassCooldown);
        }
        finally
        {
            IsCasting = false;
            activeCastCoroutine = null;
            OnCastFinished?.Invoke();
        }
    }

    private void ExecuteAbility(Ability ability, GameObject target, Vector3 position, bool bypassCooldown = false)
    {
        if (ability.abilityType != AbilityType.Charge)
        {
            PayCostAndStartCooldown(ability, bypassCooldown);
        }
        if (ability.castSound != null) AudioSource.PlayClipAtPoint(ability.castSound, transform.position);
        if (ability.castVFX != null) ObjectPooler.instance.Get(ability.castVFX, transform.position, transform.rotation);

        switch (ability.abilityType)
        {
            case AbilityType.TargetedProjectile:
            case AbilityType.ForwardProjectile:
                HandleProjectile(ability, target);
                break;
            case AbilityType.TargetedMelee:
                StartCoroutine(PerformMeleeAttack(ability));
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
                if (movementHandler != null) { movementHandler.ExecuteLeap(position, () => { HandleGroundAOE(ability, position); }); }
                break;
            case AbilityType.Charge:
                if (enemyAI != null && target != null) { PayCostAndStartCooldown(ability, bypassCooldown); enemyAI.ExecuteCharge(target, ability); }
                break;
            case AbilityType.Teleport:
                if (movementHandler != null && enemyAI != null && enemyAI.currentTarget != null)
                {
                    Vector3 directionAway = (transform.position - enemyAI.currentTarget.position).normalized;
                    Vector3 destination = transform.position + directionAway * 15f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(destination, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        movementHandler.ExecuteTeleport(hit.position);
                    }
                }
                break;
        }
    }

    private void PayCostAndStartCooldown(Ability ability, bool bypassCooldown = false)
    {
        if (!bypassCooldown)
        {
            cooldowns[ability] = Time.time + ability.cooldown;
            if (ability.triggersGlobalCooldown)
            {
                globalCooldownTimer = Time.time + globalCooldownDuration;
            }
        }
    }

    private void HandleProjectile(Ability ability, GameObject target)
    {
        GameObject prefabToSpawn = ability.enemyProjectilePrefab != null ? ability.enemyProjectilePrefab : ability.playerProjectilePrefab;
        if (prefabToSpawn == null) return;

        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        Vector3 spawnPos = spawnTransform.position;
        Quaternion spawnRot = spawnTransform.rotation;

        if (target != null) { Vector3 targetPosition = target.transform.position; Vector3 direction = targetPosition - spawnPos; direction.y = 0; if (direction != Vector3.zero) { spawnRot = Quaternion.LookRotation(direction); } }

        GameObject projectileGO = ObjectPooler.instance.Get(prefabToSpawn, spawnPos, spawnRot);
        if (projectileGO == null) return;

        projectileGO.layer = LayerMask.NameToLayer("HostileRanged");
        Collider projectileCollider = projectileGO.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            Collider[] casterColliders = GetComponentInParent<CharacterRoot>().GetComponentsInChildren<Collider>();
            foreach (Collider c in casterColliders)
            {
                Physics.IgnoreCollision(projectileCollider, c);
            }
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
            if (hitCharacter == null || affectedCharacters.Contains(hitCharacter)) { continue; }
            affectedCharacters.Add(hitCharacter);
            CharacterRoot casterRoot = this.GetComponentInParent<CharacterRoot>();
            if (casterRoot == null) return;
            int casterLayer = casterRoot.gameObject.layer;
            int targetLayer = hitCharacter.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;
            var effectsToApply = isAlly ? ability.friendlyEffects : ability.hostileEffects;
            foreach (var effect in effectsToApply) { effect.Apply(casterRoot.gameObject, hitCharacter.gameObject); }
        }
    }

    void Update() { if (ActiveBeam != null && ActiveBeam.gameObject == null) { ActiveBeam = null; } }
    public bool CanUseAbility(Ability ability, GameObject target) { if (ability == null || IsCasting) return false; if (ability.triggersGlobalCooldown && IsOnGlobalCooldown()) return false; if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false; if ((ability.abilityType == AbilityType.TargetedMelee || ability.abilityType == AbilityType.TargetedProjectile) && target == null) return false; return true; }
    private void HandleChanneledBeam(Ability ability, GameObject target) { GameObject prefabToSpawn = ability.enemyProjectilePrefab != null ? ability.enemyProjectilePrefab : ability.playerProjectilePrefab; if (prefabToSpawn != null) { GameObject beamObject = Instantiate(prefabToSpawn, transform.position, transform.rotation); if (beamObject.TryGetComponent<ChanneledBeamController>(out var beam)) { beam.Initialize(ability, this.gameObject, target); ActiveBeam = beam; } } }
    private void HandleSelfCast(Ability ability) { foreach (var effect in ability.friendlyEffects) { effect.Apply(this.gameObject, this.gameObject); } }
    private IEnumerator PerformMeleeAttack(Ability ability) { meleeHitbox.Setup(ability, this.gameObject); BoxCollider collider = meleeHitbox.GetComponent<BoxCollider>(); collider.size = ability.attackBoxSize; collider.center = ability.attackBoxCenter; meleeHitbox.gameObject.SetActive(true); yield return new WaitForSeconds(0.2f); meleeHitbox.gameObject.SetActive(false); }
}