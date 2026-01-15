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
    public bool IsInActionSequence { get; set; } = false;

    [Header("Enemy Stats & Behavior")]
    public EnemyClass enemyClass;
    public AIBehaviorProfile behaviorProfile;

    private List<HealthThresholdTrigger> triggeredHealthPhases = new List<HealthThresholdTrigger>();
    private AITargeting targeting;
    private AIAbilitySelector abilitySelector;
    private CharacterMovementHandler movementHandler;

    [Header("Leashing & State")]
    public float chaseLeashRadius = 30f;
    public PatrolPath[] patrolPaths;

    [Header("Line of Sight")]
    public float lostSightSearchDuration = 5f;
    private float lastLostSightCheckTime;
    private const float LOS_CHECK_INTERVAL = 0.2f;
    private bool cachedHasLOS = false;

    [Header("Combat Style")]
    public AIArchetype archetype;
    public float meleeAttackRange = 3f;
    public float minimumRangedAttackRange = 5f;
    public float preferredCombatRange = 15f;
    public float kiteDistance = 20f;
    public float retreatAndKiteSpeed = 8f;
    public bool isTimid = false;

    [Header("Self Preservation")]
    [Range(0f, 1f)] public float retreatHealthThreshold = 0.3f;
    [Range(0f, 1f)] public float retreatChance = 0.5f;
    public float retreatDuration = 3f;

    [Header("Surround System")]
    public float waitingDistanceOffset = 8f;

    private NavMeshAgent navMeshAgent;
    private Health health;
    private EnemyAbilityHolder abilityHolder;
    private EnemyHealthUI enemyHealthUI;
    private Animator animator;
    private AIState currentState;
    public Transform currentTarget;
    private Vector3 startPosition;
    private Vector3 combatStartPosition;
    private bool isDead = false;
    private bool hasUsedInitialAbilities = false;
    private Vector3 lastKnownPosition;
    private float timeSinceLostSight = 0f;
    public bool startDeactivated = true;
    private float retreatTimer;
    private SurroundPoint assignedSurroundPoint = null;
    private float originalSpeed;
    private PatrolPoint[] collectedPatrolPoints;
    private int currentPatrolIndex = 0;
    private bool isWaitingAtPatrolPoint = false;
    private float waitTimer = 0f;

    private string lastState = "";
    private string lastAction = "";
    private bool hasBeenActivated = false;

    // --- ANIMATION HASHES ---
    private int speedHash;
    private int attackTriggerHash;
    private int attackIndexHash;
    private int idleIndexHash;
    private int walkIndexHash;
    private bool wasMoving = false;

    private enum AIState { Idle, Combat, Returning, Retreating }

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        abilityHolder = GetComponent<EnemyAbilityHolder>();
        enemyHealthUI = GetComponentInChildren<EnemyHealthUI>();

        animator = GetComponentInChildren<Animator>();
        speedHash = Animator.StringToHash("Speed");
        attackTriggerHash = Animator.StringToHash("AttackTrigger");
        attackIndexHash = Animator.StringToHash("AttackIndex");
        idleIndexHash = Animator.StringToHash("IdleIndex");
        walkIndexHash = Animator.StringToHash("WalkIndex");

        targeting = GetComponent<AITargeting>();
        abilitySelector = GetComponent<AIAbilitySelector>();
        movementHandler = GetComponent<CharacterMovementHandler>();
    }

    void Start()
    {
        if (enemyClass != null && health != null)
        {
            health.UpdateMaxHealth(enemyClass.maxHealth);
            health.SetToMaxHealth();
            health.damageReductionPercent = enemyClass.damageMitigation;
        }

        startPosition = transform.position;
        currentState = AIState.Idle;
        originalSpeed = navMeshAgent.speed;

        if (navMeshAgent != null) navMeshAgent.updateRotation = false;

        CollectPatrolPoints();

        if (startDeactivated && !hasBeenActivated)
        {
            DeactivateAI();
        }
        else
        {
            ActivateAI();
        }

        HandleHealthChanged();
    }

    void OnEnable()
    {
        PlayerAbilityHolder.OnPlayerAbilityUsed += HandlePlayerAbilityUsed;
        if (health != null) health.OnHealthChanged += HandleHealthChanged;
        if (abilityHolder != null)
        {
            abilityHolder.OnCastStarted += HandleCastStarted;
            abilityHolder.OnCastFinished += HandleCastFinished;
        }

        if (!isDead) StartCoroutine(AIThinkRoutine());
    }

    void OnDisable()
    {
        PlayerAbilityHolder.OnPlayerAbilityUsed -= HandlePlayerAbilityUsed;
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
        if (abilityHolder != null)
        {
            abilityHolder.OnCastStarted -= HandleCastStarted;
            abilityHolder.OnCastFinished -= HandleCastFinished;
        }

        StopAllCoroutines();
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.velocity = Vector3.zero;
        }
    }

    public void ActivateAI()
    {
        hasBeenActivated = true;
        this.enabled = true;
        if (navMeshAgent != null) navMeshAgent.enabled = true;
        if (animator != null) animator.enabled = true;
    }

    public void DeactivateAI()
    {
        if (currentTarget != null) return;

        this.enabled = false;
        if (navMeshAgent != null) navMeshAgent.enabled = false;
        if (animator != null) animator.enabled = false;
    }

    private IEnumerator AIThinkRoutine()
    {
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && !navMeshAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                navMeshAgent.Warp(hit.position);
        }

        int timeout = 10;
        while (timeout > 0 && navMeshAgent != null && navMeshAgent.isActiveAndEnabled && !navMeshAgent.isOnNavMesh)
        {
            timeout--;
            yield return null;
        }

        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.2f));
        WaitForSeconds wait = new WaitForSeconds(0.1f);

        while (!isDead && this.enabled)
        {
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh && navMeshAgent.isActiveAndEnabled)
            {
                Think();
            }
            yield return wait;
        }
    }

    void Update()
    {
        HandleRotation();
        UpdateAnimator(); // Ensure this runs every frame!

        bool isSpecialMovementActive = movementHandler != null && movementHandler.IsSpecialMovementActive;
        if (IsInActionSequence || isDead || isSpecialMovementActive || abilityHolder.IsCasting || abilityHolder.ActiveBeam != null)
        {
            if (abilityHolder.IsCasting) StopMovement();
            if (abilityHolder.ActiveBeam != null && currentTarget != null)
            {
                transform.LookAt(new Vector3(currentTarget.position.x, transform.position.y, currentTarget.position.z));
            }
            return;
        }
    }

    // --- UPDATED: Hypersensitive Animation Logic ---
    private void UpdateAnimator()
    {
        if (animator == null) return;

        float currentSpeed = 0f;
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            // 1. Use desired velocity for snappy response (avoids glide start)
            Vector3 effectiveVelocity = navMeshAgent.velocity;
            if (effectiveVelocity.magnitude < 0.1f && navMeshAgent.hasPath && !navMeshAgent.isStopped)
            {
                effectiveVelocity = navMeshAgent.desiredVelocity;
            }

            float maxSpeed = navMeshAgent.speed > 0 ? navMeshAgent.speed : 3.5f;
            currentSpeed = effectiveVelocity.magnitude / maxSpeed;

            // 2. FIX: Force a minimum value if we are trying to move.
            // This ensures "Circling" (slow movement) triggers the Walk animation.
            if (navMeshAgent.hasPath && !navMeshAgent.isStopped && navMeshAgent.remainingDistance > 0.1f)
            {
                if (currentSpeed < 0.15f) currentSpeed = 0.15f;
            }
        }

        // 3. Removed the previous "0.05 clamp" so even tiny movements register.
        animator.SetFloat(speedHash, currentSpeed, 0.1f, Time.deltaTime);

        bool isMoving = currentSpeed > 0.05f;

        if (isMoving && !wasMoving)
        {
            int randomWalk = UnityEngine.Random.Range(0, 2);
            animator.SetInteger(walkIndexHash, randomWalk);
        }
        else if (!isMoving && wasMoving)
        {
            int randomIdle = UnityEngine.Random.Range(0, 3);
            animator.SetInteger(idleIndexHash, randomIdle);
        }

        wasMoving = isMoving;
    }
    // -----------------------------------------------

    private void PerformAttack(Ability ability)
    {
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(ability.overrideTriggerName))
            {
                animator.SetTrigger(ability.overrideTriggerName);
            }
            else if (ability.attackStyleIndex > 0)
            {
                animator.SetInteger(attackIndexHash, ability.attackStyleIndex);
                animator.SetTrigger(attackTriggerHash);
            }
            else
            {
                int randomAttack = UnityEngine.Random.Range(0, 3);
                animator.SetInteger(attackIndexHash, randomAttack);
                animator.SetTrigger(attackTriggerHash);
            }
        }
        abilityHolder.UseAbility(ability, currentTarget.gameObject);
    }

    private void Think()
    {
        switch (currentState)
        {
            case AIState.Idle: UpdateIdleState(); break;
            case AIState.Combat: UpdateCombatState(); break;
            case AIState.Returning: UpdateReturningState(); break;
            case AIState.Retreating: UpdateRetreatingState(); break;
        }
    }

    private void HandleRotation()
    {
        if (navMeshAgent == null || isDead) return;

        Vector3 targetLookPos = Vector3.zero;
        bool shouldRotate = false;

        if (currentState == AIState.Combat && currentTarget != null)
        {
            targetLookPos = currentTarget.position;
            shouldRotate = true;
        }
        else if (navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh && navMeshAgent.velocity.sqrMagnitude > 0.1f)
        {
            targetLookPos = transform.position + navMeshAgent.velocity;
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

    private void UpdateCombatState()
    {
        if (currentTarget == null || Vector3.Distance(transform.position, combatStartPosition) > chaseLeashRadius)
        {
            ResetCombatState();
            currentState = AIState.Returning;
            navMeshAgent.SetDestination(startPosition);
            return;
        }

        if (IsTargetInvalid(currentTarget))
        {
            ResetCombatState();
            currentState = AIState.Idle;
            return;
        }

        if (health.currentHealth / (float)health.maxHealth < retreatHealthThreshold)
        {
            if (UnityEngine.Random.value < retreatChance)
            {
                currentState = AIState.Retreating;
                retreatTimer = retreatDuration;
                if (assignedSurroundPoint != null)
                {
                    SurroundPointManager.instance.ReleasePoint(this);
                    assignedSurroundPoint = null;
                }
                return;
            }
        }

        float distToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (Time.time - lastLostSightCheckTime > LOS_CHECK_INTERVAL || distToTarget < meleeAttackRange)
        {
            cachedHasLOS = HasLineOfSight(currentTarget);
            lastLostSightCheckTime = Time.time;
        }

        if (cachedHasLOS)
        {
            lastKnownPosition = currentTarget.position;
            timeSinceLostSight = 0f;

            Ability abilityToUse = abilitySelector.SelectBestAbility(currentTarget, hasUsedInitialAbilities);
            bool isReady = abilityToUse != null && abilityHolder.CanUseAbility(abilityToUse, currentTarget.gameObject);
            float effectiveRange = (abilityToUse != null) ? abilityToUse.range : meleeAttackRange;

            if (isReady && distToTarget <= effectiveRange)
            {
                StopMovement();
                SetAIStatus("Combat", $"Attacking: {abilityToUse.displayName}");
                PerformAttack(abilityToUse);

                if (abilitySelector.initialAbilities.Contains(abilityToUse)) hasUsedInitialAbilities = true;
            }
            else
            {
                HandleCombatMovement(isReady, abilityToUse);
            }
        }
        else
        {
            if (isTimid) { currentTarget = null; return; }
            timeSinceLostSight += 0.1f;
            SetAIStatus("Combat", "Searching...");
            if (timeSinceLostSight > lostSightSearchDuration) currentTarget = null;
            else navMeshAgent.SetDestination(lastKnownPosition);
        }
    }

    private bool IsTargetInvalid(Transform target)
    {
        if (target == null) return true;
        Health h = null;
        CharacterRoot root = target.GetComponent<CharacterRoot>() ?? target.GetComponentInParent<CharacterRoot>();
        if (root != null) h = root.Health;
        else h = target.GetComponent<Health>() ?? target.GetComponentInParent<Health>() ?? target.GetComponentInChildren<Health>();
        return h == null || h.isDowned || h.currentHealth <= 0;
    }

    private void HandleCombatMovement(bool isReady, Ability abilityToUse)
    {
        bool isTargetDomeMarker = currentTarget.CompareTag("DomeMarker");
        if (isTargetDomeMarker)
        {
            ExecuteDirectMovement();
        }
        else if (isReady && archetype == AIArchetype.Melee)
        {
            ExecuteClosingMovement(abilityToUse);
        }
        else
        {
            switch (archetype)
            {
                case AIArchetype.Melee: ExecuteMeleeMovement(); break;
                case AIArchetype.Ranged: ExecuteRangedMovement(); break;
                case AIArchetype.Hybrid:
                    if (isReady && abilityToUse.range <= meleeAttackRange) ExecuteClosingMovement(abilityToUse);
                    else ExecuteRangedMovement();
                    break;
            }
        }
    }

    private void ExecuteClosingMovement(Ability ability)
    {
        navMeshAgent.speed = originalSpeed;
        if (assignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; }
        float range = (ability != null) ? ability.range : meleeAttackRange;
        navMeshAgent.stoppingDistance = range * 0.8f;
        navMeshAgent.SetDestination(currentTarget.position);
        SetAIStatus("Combat", "Closing In");
    }

    private void ExecuteDirectMovement()
    {
        navMeshAgent.speed = originalSpeed;
        Ability ability = abilitySelector.SelectBestAbility(currentTarget, hasUsedInitialAbilities) ?? abilitySelector.abilities.FirstOrDefault();
        float range = (ability != null) ? ability.range : meleeAttackRange;
        navMeshAgent.stoppingDistance = range * 0.8f;
        navMeshAgent.SetDestination(currentTarget.position);
        SetAIStatus("Combat", "Advancing on Dome");
    }

    private void ExecuteMeleeMovement()
    {
        navMeshAgent.speed = originalSpeed;
        if (assignedSurroundPoint == null) assignedSurroundPoint = SurroundPointManager.instance.RequestPoint(this, currentTarget);
        Vector3 destination = assignedSurroundPoint != null ? assignedSurroundPoint.position : currentTarget.position;
        navMeshAgent.stoppingDistance = 0.5f;
        navMeshAgent.SetDestination(destination);
        SetAIStatus("Combat", assignedSurroundPoint != null ? "Circling" : "Waiting");
    }

    private void UpdateIdleState()
    {
        currentTarget = targeting.FindBestTarget(null);
        if (currentTarget != null)
        {
            hasUsedInitialAbilities = false;
            currentState = AIState.Combat;
            combatStartPosition = transform.position;
            lastKnownPosition = currentTarget.position;
            if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
            return;
        }

        if (collectedPatrolPoints == null || collectedPatrolPoints.Length == 0)
        {
            SetAIStatus("Idle", "Searching...");
            return;
        }

        if (isWaitingAtPatrolPoint)
        {
            SetAIStatus("Idle", "Waiting");
            waitTimer -= 0.1f;
            if (waitTimer <= 0)
            {
                isWaitingAtPatrolPoint = false;
                AdvancePatrolIndex();
                navMeshAgent.SetDestination(collectedPatrolPoints[currentPatrolIndex].transform.position);
            }
        }
        else if (navMeshAgent.isOnNavMesh && !navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f)
        {
            PatrolPoint currentPoint = collectedPatrolPoints[currentPatrolIndex];
            if (!string.IsNullOrEmpty(currentPoint.animationTriggerName) && animator != null) animator.SetTrigger(currentPoint.animationTriggerName);
            if (currentPoint.onArrive != null) currentPoint.onArrive.Invoke();
            if (!string.IsNullOrEmpty(currentPoint.sendMessageToNPC)) SendMessage(currentPoint.sendMessageToNPC, SendMessageOptions.DontRequireReceiver);

            float waitTime = UnityEngine.Random.Range(currentPoint.minWaitTime, currentPoint.maxWaitTime);
            if (waitTime > 0)
            {
                isWaitingAtPatrolPoint = true;
                waitTimer = waitTime;
            }
            else
            {
                AdvancePatrolIndex();
                navMeshAgent.SetDestination(collectedPatrolPoints[currentPatrolIndex].transform.position);
            }
        }
        else
        {
            SetAIStatus("Idle", "Patrolling");
        }
    }

    private void AdvancePatrolIndex()
    {
        PatrolPoint lastPoint = collectedPatrolPoints[currentPatrolIndex];
        if (lastPoint.nextPointOverride != null)
        {
            int nextIndex = Array.IndexOf(collectedPatrolPoints, lastPoint.nextPointOverride);
            currentPatrolIndex = (nextIndex >= 0) ? nextIndex : 0;
        }
        else if (lastPoint.jumpToRandomPoint)
        {
            currentPatrolIndex = UnityEngine.Random.Range(0, collectedPatrolPoints.Length);
        }
        else
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % collectedPatrolPoints.Length;
        }
    }

    private void UpdateReturningState()
    {
        navMeshAgent.speed = originalSpeed;
        SetAIStatus("Returning", "Leashing");
        currentTarget = targeting.FindBestTarget(null);
        if (currentTarget != null)
        {
            currentState = AIState.Combat;
            combatStartPosition = transform.position;
            return;
        }
        if (navMeshAgent.isOnNavMesh && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            navMeshAgent.ResetPath();
            currentState = AIState.Idle;
        }
    }

    private void UpdateRetreatingState()
    {
        retreatTimer -= 0.1f;
        if (retreatTimer > 0 && currentTarget != null)
        {
            SetAIStatus("Combat", "Retreating");
            navMeshAgent.speed = retreatAndKiteSpeed;
            RetreatFromTarget();
        }
        else
        {
            navMeshAgent.speed = originalSpeed;
            currentState = AIState.Combat;
        }
    }

    private void StopMovement()
    {
        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.velocity = Vector3.zero;
            if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
        }
    }

    private void ResetCombatState() { hasUsedInitialAbilities = false; if (assignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; } if (abilityHolder.ActiveBeam != null) abilityHolder.ActiveBeam.Interrupt(); currentTarget = null; }
    private void ExecuteRangedMovement() { float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position); if (distanceToTarget < minimumRangedAttackRange) { navMeshAgent.speed = retreatAndKiteSpeed; RetreatFromTarget(); } else { navMeshAgent.speed = originalSpeed; if (distanceToTarget > preferredCombatRange) { Vector3 destination = currentTarget.position - (currentTarget.position - transform.position).normalized * preferredCombatRange; navMeshAgent.SetDestination(destination); SetAIStatus("Combat", "Advancing"); } else { navMeshAgent.ResetPath(); SetAIStatus("Combat", "In Range"); } } }

    private void RetreatFromTarget()
    {
        if (currentTarget == null) return;
        Vector3 directionAwayFromTarget = (transform.position - currentTarget.position).normalized;
        float bestPathLength = 0;
        Vector3 bestRetreatPoint = Vector3.zero;
        bool foundRetreatPoint = false;
        int numSamples = 8;
        float retreatArc = 120f;
        for (int i = 0; i < numSamples; i++) { float angle = (i / (float)(numSamples - 1) - 0.5f) * retreatArc; Vector3 sampleDirection = Quaternion.Euler(0, angle, 0) * directionAwayFromTarget; Vector3 potentialDestination = transform.position + sampleDirection * kiteDistance; if (NavMesh.SamplePosition(potentialDestination, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) { NavMeshPath path = new NavMeshPath(); if (navMeshAgent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete) { float pathLength = 0f; for (int j = 1; j < path.corners.Length; j++) { pathLength += Vector3.Distance(path.corners[j - 1], path.corners[j]); } if (pathLength > bestPathLength) { bestPathLength = pathLength; bestRetreatPoint = hit.position; foundRetreatPoint = true; } } } }
        if (foundRetreatPoint) { navMeshAgent.SetDestination(bestRetreatPoint); }
    }

    private bool HasLineOfSight(Transform target)
    {
        if (targeting != null && !targeting.checkLineOfSight) return true;
        if (target == null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 targetPosition = target.position + Vector3.up;
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, targeting.obstacleLayers))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target)) return true;
            return false;
        }
        return true;
    }

    private void HandleHealthChanged() { if (health.currentHealth <= 0 && !isDead) { isDead = true; if (assignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; } navMeshAgent.enabled = false; this.enabled = false; StopAllCoroutines(); return; } if (behaviorProfile != null && !isDead) { float currentHealthPercent = (float)health.currentHealth / health.maxHealth; foreach (var trigger in behaviorProfile.healthTriggers) { if (currentHealthPercent <= trigger.healthPercentage && !triggeredHealthPhases.Contains(trigger)) { triggeredHealthPhases.Add(trigger); GameObject targetForAbility = (currentTarget != null) ? currentTarget.gameObject : this.gameObject; abilityHolder.UseAbility(trigger.abilityToUse, targetForAbility); break; } } } }
    private void HandlePlayerAbilityUsed(PlayerAbilityHolder player, Ability usedAbility) { PlayerMovement playerMovement = player.GetComponentInParent<PlayerMovement>(); if (isDead || abilityHolder.IsCasting || playerMovement == null || playerMovement.TargetObject != this.gameObject) return; if (behaviorProfile != null) { foreach (var trigger in behaviorProfile.reactiveTriggers) { if (trigger.triggerType == usedAbility.abilityType) { if (UnityEngine.Random.value <= trigger.chanceToReact) { abilityHolder.UseAbility(trigger.reactionAbility, player.gameObject); break; } } } } }
    private void HandleCastStarted(string abilityName, float castDuration) { if (enemyHealthUI != null) enemyHealthUI.StartCast(abilityName, castDuration); SetAIStatus("Combat", "Casting"); if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); }
    private void HandleCastFinished() { if (enemyHealthUI != null) enemyHealthUI.StopCast(); }

    public void ExecuteLeap(Vector3 destination, Action onLandAction) { movementHandler?.ExecuteLeap(destination, onLandAction); }
    public void ExecuteCharge(GameObject target, Ability chargeAbility) { if (abilitySelector.abilities.Contains(chargeAbility)) movementHandler?.ExecuteCharge(target, chargeAbility); }
    public void ExecuteTeleport(Vector3 destination) { if (navMeshAgent.isOnNavMesh) navMeshAgent.Warp(destination); else transform.position = destination; }

    private void SetAIStatus(string state, string action)
    {
        if (lastState == state && lastAction == action) return;
        lastState = state;
        lastAction = action;
        enemyHealthUI?.UpdateStatus(state, action);
        if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowAIStatus(action, transform.position + Vector3.up * 3.5f);
    }

    private void CollectPatrolPoints() { if (patrolPaths == null || patrolPaths.Length == 0) return; List<PatrolPoint> points = new List<PatrolPoint>(); foreach (PatrolPath path in patrolPaths) { if (path == null) continue; Collider pathCollider = path.GetComponent<Collider>(); if (pathCollider == null) continue; Collider[] collidersInVolume = Physics.OverlapBox(path.transform.position, pathCollider.bounds.extents, path.transform.rotation); foreach (Collider col in collidersInVolume) { if (col.TryGetComponent<PatrolPoint>(out PatrolPoint point)) { if (!points.Contains(point)) points.Add(point); } } } collectedPatrolPoints = points.OrderBy(p => p.gameObject.name).ToArray(); }
    void OnDestroy() { if (assignedSurroundPoint != null && SurroundPointManager.instance != null) { SurroundPointManager.instance.ReleasePoint(this); } if (health != null) health.OnHealthChanged -= HandleHealthChanged; if (abilityHolder != null) { abilityHolder.OnCastStarted -= HandleCastStarted; abilityHolder.OnCastFinished -= HandleCastFinished; } }
}