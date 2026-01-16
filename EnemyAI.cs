using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(EnemyAbilityHolder))]
[RequireComponent(typeof(AITargeting), typeof(AIAbilitySelector))]
public class EnemyAI : MonoBehaviour, IMovementHandler
{
    // --- Public Properties for State Access ---
    public NavMeshAgent NavAgent { get; private set; }
    public Health Health { get; private set; }
    public EnemyAbilityHolder AbilityHolder { get; private set; }
    public AITargeting Targeting { get; private set; }
    public AIAbilitySelector AbilitySelector { get; private set; }
    public Animator Animator { get; private set; }
    public CharacterMovementHandler MovementHandler { get; private set; }

    // --- Additional Components to Cleanup ---
    private MonoBehaviour lootGen; // Stored as MonoBehaviour to avoid dependency errors if script is missing
    private MonoBehaviour statusEffectHolder;
    private CharacterRoot characterRoot;

    // --- State Data ---
    public Transform currentTarget;
    public Vector3 StartPosition { get; private set; }
    public Vector3 CombatStartPosition { get; set; }
    public bool HasUsedInitialAbilities { get; set; }
    public SurroundPoint AssignedSurroundPoint { get; set; }
    public PatrolPoint[] CollectedPatrolPoints { get; private set; }
    public int CurrentPatrolIndex { get; set; } = 0;
    public bool IsWaitingAtPatrolPoint { get; set; } = false;
    public float OriginalSpeed { get; private set; }

    public bool IsInActionSequence { get; set; } = false;

    // --- Configuration ---
    [Header("Enemy Stats & Behavior")]
    public EnemyClass enemyClass;
    public AIBehaviorProfile behaviorProfile;

    [Header("Leashing & State")]
    public float chaseLeashRadius = 30f;
    public PatrolPath[] patrolPaths;

    [Header("Line of Sight")]
    public float lostSightSearchDuration = 5f;

    [Header("Combat Style")]
    public AIArchetype archetype;
    public float meleeAttackRange = 3f;
    public float minimumRangedAttackRange = 5f;
    public float preferredCombatRange = 15f;
    public float kiteDistance = 20f;
    public float retreatAndKiteSpeed = 8f;
    public bool isTimid = false;

    [Header("Combat Timing")]
    public float attackRecoveryDelay = 0.5f;
    private float recoveryTimer = 0f;

    [Header("Death & Cleanup")]
    [Tooltip("How long the body stays visible before being destroyed.")]
    public float corpseDuration = 5.0f;

    [Header("Self Preservation")]
    [Range(0f, 1f)] public float retreatHealthThreshold = 0.3f;
    [Range(0f, 1f)] public float retreatChance = 0.5f;
    public float retreatDuration = 3f;

    private List<HealthThresholdTrigger> triggeredHealthPhases = new List<HealthThresholdTrigger>();
    private EnemyHealthUI enemyHealthUI;
    private bool isDead = false;
    public bool startDeactivated = true;
    private bool hasBeenActivated = false;

    // --- UI State Tracking ---
    private string lastState = "";
    private string lastAction = "";

    // --- State Machine ---
    private IEnemyState currentState;

    // --- Animation Hashes ---
    private int speedHash;
    private int attackTriggerHash;
    private int attackIndexHash;
    private int idleIndexHash;
    private int walkIndexHash;
    private bool wasMoving = false;

    void Awake()
    {
        NavAgent = GetComponent<NavMeshAgent>();
        Health = GetComponent<Health>();
        AbilityHolder = GetComponent<EnemyAbilityHolder>();
        enemyHealthUI = GetComponentInChildren<EnemyHealthUI>();
        Animator = GetComponentInChildren<Animator>();
        Targeting = GetComponent<AITargeting>();
        AbilitySelector = GetComponent<AIAbilitySelector>();
        MovementHandler = GetComponent<CharacterMovementHandler>();
        characterRoot = GetComponent<CharacterRoot>();

        // Look for optional components by name string to avoid errors if you haven't created the scripts yet
        // OR simply try GetComponent if they exist in your project.
        // Assuming the class names "LootGen" and "StatusEffectHolder" exist:
        lootGen = GetComponent("LootGen") as MonoBehaviour;
        statusEffectHolder = GetComponent("StatusEffectHolder") as MonoBehaviour;

        speedHash = Animator.StringToHash("Speed");
        attackTriggerHash = Animator.StringToHash("AttackTrigger");
        attackIndexHash = Animator.StringToHash("AttackIndex");
        idleIndexHash = Animator.StringToHash("IdleIndex");
        walkIndexHash = Animator.StringToHash("WalkIndex");
    }

    void Start()
    {
        if (enemyClass != null && Health != null)
        {
            Health.UpdateMaxHealth(enemyClass.maxHealth);
            Health.SetToMaxHealth();
            Health.damageReductionPercent = enemyClass.damageMitigation;
        }

        StartPosition = transform.position;
        OriginalSpeed = NavAgent.speed;

        if (NavAgent != null) NavAgent.updateRotation = false;

        CollectPatrolPoints();
        SwitchState(new IdleState());

        if (startDeactivated && !hasBeenActivated) DeactivateAI();
        else ActivateAI();

        HandleHealthChanged();
    }

    void OnEnable()
    {
        PlayerAbilityHolder.OnPlayerAbilityUsed += HandlePlayerAbilityUsed;
        if (Health != null) { Health.OnHealthChanged += HandleHealthChanged; Health.OnDeath += OnDeath; }
        if (AbilityHolder != null) { AbilityHolder.OnCastStarted += HandleCastStarted; AbilityHolder.OnCastFinished += HandleCastFinished; }
        if (!isDead) StartCoroutine(AIThinkRoutine());
    }

    void OnDisable()
    {
        PlayerAbilityHolder.OnPlayerAbilityUsed -= HandlePlayerAbilityUsed;
        if (Health != null) { Health.OnHealthChanged -= HandleHealthChanged; Health.OnDeath -= OnDeath; }
        if (AbilityHolder != null) { AbilityHolder.OnCastStarted -= HandleCastStarted; AbilityHolder.OnCastFinished -= HandleCastFinished; }
        StopAllCoroutines();
        if (NavAgent != null && NavAgent.isActiveAndEnabled && NavAgent.isOnNavMesh) { NavAgent.ResetPath(); NavAgent.velocity = Vector3.zero; }
    }

    // --- State Machine Logic ---
    public void SwitchState(IEnemyState newState)
    {
        currentState?.Exit(this);
        currentState = newState;
        currentState?.Enter(this);
    }

    private IEnumerator AIThinkRoutine()
    {
        if (NavAgent != null && NavAgent.isActiveAndEnabled && !NavAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                NavAgent.Warp(hit.position);
        }

        int timeout = 10;
        while (timeout > 0 && NavAgent != null && NavAgent.isActiveAndEnabled && !NavAgent.isOnNavMesh)
        {
            timeout--;
            yield return null;
        }

        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.2f));
        WaitForSeconds wait = new WaitForSeconds(0.1f);

        while (!isDead && this.enabled)
        {
            if (NavAgent != null && NavAgent.isOnNavMesh && NavAgent.isActiveAndEnabled)
            {
                Think();
            }
            yield return wait;
        }
    }

    private void Think()
    {
        if (Time.time < recoveryTimer) return;
        currentState?.Execute(this);
    }

    void Update()
    {
        if (isDead) return;

        HandleRotation();

        // 1. Check Animation Locks FIRST
        bool isAnimationLocked = false;

        if (Animator != null)
        {
            AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag("Attack"))
            {
                isAnimationLocked = true;
            }
            else if (Animator.IsInTransition(0))
            {
                AnimatorTransitionInfo transInfo = Animator.GetAnimatorTransitionInfo(0);
                if (transInfo.anyState && stateInfo.IsTag("Attack")) isAnimationLocked = true;
            }
        }

        bool isRecovering = Time.time < recoveryTimer;

        // 2. Enforce Lock
        if (isAnimationLocked || isRecovering)
        {
            StopMovement();
            if (Animator != null) Animator.SetFloat(speedHash, 0f);
            return;
        }

        // 3. Normal Update
        UpdateAnimator();

        bool isSpecialMovementActive = MovementHandler != null && MovementHandler.IsSpecialMovementActive;
        if (IsInActionSequence || isDead || isSpecialMovementActive || AbilityHolder.IsCasting || AbilityHolder.ActiveBeam != null)
        {
            if (AbilityHolder.IsCasting) StopMovement();
            if (AbilityHolder.ActiveBeam != null && currentTarget != null)
            {
                transform.LookAt(new Vector3(currentTarget.position.x, transform.position.y, currentTarget.position.z));
            }
            return;
        }
    }

    // --- Shared Logic called by States ---

    public void PerformAttack(Ability ability)
    {
        if (Animator != null)
        {
            if (!string.IsNullOrEmpty(ability.overrideTriggerName))
            {
                Animator.SetTrigger(ability.overrideTriggerName);
            }
            else if (ability.attackStyleIndex > 0)
            {
                Animator.SetInteger(attackIndexHash, ability.attackStyleIndex);
                Animator.SetTrigger(attackTriggerHash);
            }
            else
            {
                int randomAttack = UnityEngine.Random.Range(0, 3);
                Animator.SetInteger(attackIndexHash, randomAttack);
                Animator.SetTrigger(attackTriggerHash);
            }
        }
        AbilityHolder.UseAbility(ability, currentTarget.gameObject);
    }

    public void StopMovement()
    {
        if (NavAgent != null && NavAgent.isActiveAndEnabled && NavAgent.isOnNavMesh)
        {
            NavAgent.ResetPath();
            NavAgent.velocity = Vector3.zero;
        }
    }

    public void RetreatFromTarget()
    {
        if (NavAgent == null || !NavAgent.isActiveAndEnabled || !NavAgent.isOnNavMesh) return;

        if (currentTarget == null) return;
        Vector3 directionAwayFromTarget = (transform.position - currentTarget.position).normalized;
        float bestPathLength = 0;
        Vector3 bestRetreatPoint = Vector3.zero;
        bool foundRetreatPoint = false;
        int numSamples = 8;
        float retreatArc = 120f;
        for (int i = 0; i < numSamples; i++) { float angle = (i / (float)(numSamples - 1) - 0.5f) * retreatArc; Vector3 sampleDirection = Quaternion.Euler(0, angle, 0) * directionAwayFromTarget; Vector3 potentialDestination = transform.position + sampleDirection * kiteDistance; if (NavMesh.SamplePosition(potentialDestination, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) { NavMeshPath path = new NavMeshPath(); if (NavAgent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete) { float pathLength = 0f; for (int j = 1; j < path.corners.Length; j++) { pathLength += Vector3.Distance(path.corners[j - 1], path.corners[j]); } if (pathLength > bestPathLength) { bestPathLength = pathLength; bestRetreatPoint = hit.position; foundRetreatPoint = true; } } } }
        if (foundRetreatPoint) { NavAgent.SetDestination(bestRetreatPoint); }
    }

    public bool IsTargetInvalid(Transform target)
    {
        if (target == null) return true;
        Health h = null;
        CharacterRoot root = target.GetComponent<CharacterRoot>() ?? target.GetComponentInParent<CharacterRoot>();
        if (root != null) h = root.Health;
        else h = target.GetComponent<Health>() ?? target.GetComponentInParent<Health>() ?? target.GetComponentInChildren<Health>();
        return h == null || h.isDowned || h.currentHealth <= 0;
    }

    public bool HasLineOfSight(Transform target)
    {
        if (Targeting != null && !Targeting.checkLineOfSight) return true;
        if (target == null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 targetPosition = target.position + Vector3.up;
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, Targeting.obstacleLayers))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target)) return true;
            return false;
        }
        return true;
    }

    public void ResetCombatState()
    {
        HasUsedInitialAbilities = false;
        if (AssignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); AssignedSurroundPoint = null; }
        if (AbilityHolder.ActiveBeam != null) AbilityHolder.ActiveBeam.Interrupt();
        currentTarget = null;
    }

    public void SetAIStatus(string state, string action)
    {
        if (lastState == state && lastAction == action) return;
        lastState = state;
        lastAction = action;
        enemyHealthUI?.UpdateStatus(state, action);
        if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowAIStatus(action, transform.position + Vector3.up * 3.5f);
    }

    private void HandleRotation()
    {
        if (NavAgent == null || isDead) return;

        Vector3 targetLookPos = Vector3.zero;
        bool shouldRotate = false;
        bool inCombat = (currentTarget != null);

        if (inCombat || Time.time < recoveryTimer)
        {
            if (currentTarget != null)
            {
                targetLookPos = currentTarget.position;
                shouldRotate = true;
            }
        }
        else if (NavAgent.isActiveAndEnabled && NavAgent.isOnNavMesh && NavAgent.velocity.sqrMagnitude > 0.1f)
        {
            targetLookPos = transform.position + NavAgent.velocity;
            shouldRotate = true;
        }

        if (shouldRotate)
        {
            Vector3 direction = (targetLookPos - transform.position).normalized;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
            }
        }
    }

    private void UpdateAnimator()
    {
        if (Animator == null) return;
        if (NavAgent == null || !NavAgent.isActiveAndEnabled || !NavAgent.isOnNavMesh)
        {
            Animator.SetFloat(speedHash, 0f);
            return;
        }

        float currentSpeed = 0f;
        if (NavAgent != null && NavAgent.isActiveAndEnabled && NavAgent.isOnNavMesh)
        {
            Vector3 effectiveVelocity = NavAgent.velocity;
            if (effectiveVelocity.magnitude < 0.1f && NavAgent.hasPath && !NavAgent.isStopped)
            {
                effectiveVelocity = NavAgent.desiredVelocity;
            }

            float maxSpeed = NavAgent.speed > 0 ? NavAgent.speed : 3.5f;
            currentSpeed = effectiveVelocity.magnitude / maxSpeed;

            if (NavAgent.hasPath && !NavAgent.isStopped && NavAgent.remainingDistance > 0.1f)
            {
                if (currentSpeed < 0.15f) currentSpeed = 0.15f;
            }
        }

        Animator.SetFloat(speedHash, currentSpeed, 0f, Time.deltaTime);

        bool isMoving = currentSpeed > 0.05f;
        if (isMoving && !wasMoving)
        {
            int randomWalk = UnityEngine.Random.Range(0, 2);
            Animator.SetInteger(walkIndexHash, randomWalk);
        }
        else if (!isMoving && wasMoving)
        {
            int randomIdle = UnityEngine.Random.Range(0, 3);
            Animator.SetInteger(idleIndexHash, randomIdle);
        }
        wasMoving = isMoving;
    }

    // --- Death Handling ---

    private void OnDeath()
    {
        isDead = true;

        // 1. Start Cleanup (Must do this BEFORE disabling 'this')
        // Coroutines run on the GameObject, but starting them usually requires an active component instance.
        StartCoroutine(HandleCorpseCleanup());

        // 2. Disable Logic Components
        StopMovement();
        if (NavAgent != null) NavAgent.enabled = false;

        Collider c = GetComponent<Collider>();
        if (c != null) c.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (AbilityHolder != null) AbilityHolder.enabled = false;
        if (Targeting != null) Targeting.enabled = false;
        if (AbilitySelector != null) AbilitySelector.enabled = false;
        if (MovementHandler != null) MovementHandler.enabled = false;
        if (enemyHealthUI != null) enemyHealthUI.gameObject.SetActive(false);

        // Disable the extra components found in Awake
        if (lootGen != null) lootGen.enabled = false;
        if (statusEffectHolder != null) statusEffectHolder.enabled = false;
        if (characterRoot != null) characterRoot.enabled = false;

        // Disable Health last (visuals might rely on it momentarily)
        if (Health != null) Health.enabled = false;

        // 3. Handle Animation
        if (Animator != null)
        {
            Animator.SetFloat(speedHash, 0f);
            Animator.ResetTrigger(attackTriggerHash);
        }

        SetAIStatus("Dead", "");

        // 4. Disable THIS script (stops Update loop, keeps Inspector clean)
        this.enabled = false;
    }

    private IEnumerator HandleCorpseCleanup()
    {
        yield return new WaitForSeconds(corpseDuration);

        // Placeholder for future VFX
        // Instantiate(meltVFX, transform.position, ...);

        Destroy(gameObject);
    }

    public void ActivateAI()
    {
        hasBeenActivated = true;
        this.enabled = true;
        if (NavAgent != null) NavAgent.enabled = true;
        if (Animator != null) Animator.enabled = true;
    }

    public void DeactivateAI()
    {
        if (currentTarget != null) return;
        this.enabled = false;
        if (NavAgent != null) NavAgent.enabled = false;
        if (Animator != null) Animator.enabled = false;
    }

    private void CollectPatrolPoints()
    {
        if (patrolPaths == null || patrolPaths.Length == 0) return;
        List<PatrolPoint> points = new List<PatrolPoint>();
        foreach (PatrolPath path in patrolPaths)
        {
            if (path == null) continue;
            Collider pathCollider = path.GetComponent<Collider>();
            if (pathCollider == null) continue;
            Collider[] collidersInVolume = Physics.OverlapBox(path.transform.position, pathCollider.bounds.extents, path.transform.rotation);
            foreach (Collider col in collidersInVolume)
            {
                if (col.TryGetComponent<PatrolPoint>(out PatrolPoint point))
                {
                    if (!points.Contains(point)) points.Add(point);
                }
            }
        }
        CollectedPatrolPoints = points.OrderBy(p => p.gameObject.name).ToArray();
    }

    private void HandleHealthChanged()
    {
        if (Health.currentHealth <= 0 && !isDead) { OnDeath(); }
        if (behaviorProfile != null && !isDead)
        {
            float currentHealthPercent = (float)Health.currentHealth / Health.maxHealth;
            foreach (var trigger in behaviorProfile.healthTriggers)
            {
                if (currentHealthPercent <= trigger.healthPercentage && !triggeredHealthPhases.Contains(trigger))
                {
                    triggeredHealthPhases.Add(trigger);
                    GameObject targetForAbility = (currentTarget != null) ? currentTarget.gameObject : this.gameObject;
                    AbilityHolder.UseAbility(trigger.abilityToUse, targetForAbility);
                    break;
                }
            }
        }
    }

    private void HandlePlayerAbilityUsed(PlayerAbilityHolder player, Ability usedAbility)
    {
        PlayerMovement playerMovement = player.GetComponentInParent<PlayerMovement>();
        if (isDead || AbilityHolder.IsCasting || playerMovement == null || playerMovement.TargetObject != this.gameObject) return;
        if (behaviorProfile != null)
        {
            foreach (var trigger in behaviorProfile.reactiveTriggers)
            {
                if (trigger.triggerType == usedAbility.abilityType)
                {
                    if (UnityEngine.Random.value <= trigger.chanceToReact)
                    {
                        AbilityHolder.UseAbility(trigger.reactionAbility, player.gameObject);
                        break;
                    }
                }
            }
        }
    }

    private void HandleCastStarted(string abilityName, float castDuration)
    {
        if (enemyHealthUI != null) enemyHealthUI.StartCast(abilityName, castDuration);
        SetAIStatus("Combat", "Casting");
        if (NavAgent.isOnNavMesh) NavAgent.ResetPath();
    }

    private void HandleCastFinished()
    {
        if (enemyHealthUI != null) enemyHealthUI.StopCast();
        recoveryTimer = Time.time + attackRecoveryDelay;
    }

    public void ExecuteLeap(Vector3 destination, Action onLandAction) { MovementHandler?.ExecuteLeap(destination, onLandAction); }
    public void ExecuteCharge(GameObject target, Ability chargeAbility) { if (AbilitySelector.abilities.Contains(chargeAbility)) MovementHandler?.ExecuteCharge(target, chargeAbility); }
    public void ExecuteTeleport(Vector3 destination) { if (NavAgent.isOnNavMesh) NavAgent.Warp(destination); else transform.position = destination; }

    void OnDestroy()
    {
        if (AssignedSurroundPoint != null && SurroundPointManager.instance != null) { SurroundPointManager.instance.ReleasePoint(this); }
        if (Health != null) { Health.OnHealthChanged -= HandleHealthChanged; Health.OnDeath -= OnDeath; }
        if (AbilityHolder != null) { AbilityHolder.OnCastStarted -= HandleCastStarted; AbilityHolder.OnCastFinished -= HandleCastFinished; }
    }
}