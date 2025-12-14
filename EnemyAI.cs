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
    private string lastStatusMessage;

    private enum AIState { Idle, Combat, Returning, Retreating }

    void OnEnable() { PlayerAbilityHolder.OnPlayerAbilityUsed += HandlePlayerAbilityUsed; }
    void OnDisable() { PlayerAbilityHolder.OnPlayerAbilityUsed -= HandlePlayerAbilityUsed; }

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        abilityHolder = GetComponent<EnemyAbilityHolder>();
        enemyHealthUI = GetComponentInChildren<EnemyHealthUI>();
        animator = GetComponentInChildren<Animator>();
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
        health.OnHealthChanged += HandleHealthChanged;

        if (abilityHolder != null)
        {
            abilityHolder.OnCastStarted += HandleCastStarted;
            abilityHolder.OnCastFinished += HandleCastFinished;
        }

        if (startDeactivated) DeactivateAI();
        HandleHealthChanged();
    }

    void Update()
    {
        HandleRotation();

        bool isSpecialMovementActive = movementHandler != null && movementHandler.IsSpecialMovementActive;
        if (IsInActionSequence || isDead || isSpecialMovementActive || abilityHolder.IsCasting || abilityHolder.ActiveBeam != null)
        {
            if (abilityHolder.IsCasting)
            {
                navMeshAgent.velocity = Vector3.zero;
                if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
            }
            if (abilityHolder.ActiveBeam != null && currentTarget != null)
            {
                transform.LookAt(new Vector3(currentTarget.position.x, transform.position.y, currentTarget.position.z));
            }
            return;
        }

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
        else if (navMeshAgent.velocity.sqrMagnitude > 0.1f)
        {
            targetLookPos = transform.position + navMeshAgent.velocity;
            shouldRotate = true;
        }

        if (shouldRotate)
        {
            Vector3 direction = (targetLookPos - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
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

        // --- Aggro Validation (CharacterRoot + Fallback) ---
        Health targetHealth = null;
        CharacterRoot root = currentTarget.GetComponentInParent<CharacterRoot>();

        if (root != null)
        {
            targetHealth = root.Health;
        }
        else
        {
            targetHealth = currentTarget.GetComponent<Health>() ?? currentTarget.GetComponentInParent<Health>();
        }

        // Drop target if dead/downed
        if (targetHealth != null && (targetHealth.isDowned || targetHealth.currentHealth <= 0))
        {
            ResetCombatState();
            currentState = AIState.Idle;
            return;
        }
        // ---------------------------------------------------

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

        if (HasLineOfSight(currentTarget))
        {
            lastKnownPosition = currentTarget.position;
            timeSinceLostSight = 0f;

            Ability abilityToUse = abilitySelector.SelectBestAbility(currentTarget, hasUsedInitialAbilities);
            bool isReady = abilityToUse != null;
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
            float effectiveRange = isReady ? abilityToUse.range : meleeAttackRange;
            bool isInRange = distanceToTarget <= effectiveRange;

            if (isReady && isInRange)
            {
                navMeshAgent.ResetPath();
                navMeshAgent.velocity = Vector3.zero;

                SetAIStatus("Combat", $"Attacking: {abilityToUse.displayName}");
                abilityHolder.UseAbility(abilityToUse, currentTarget.gameObject);

                if (abilitySelector.initialAbilities.Contains(abilityToUse))
                {
                    hasUsedInitialAbilities = true;
                }
            }
            else
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
                        case AIArchetype.Melee:
                            ExecuteMeleeMovement();
                            break;
                        case AIArchetype.Ranged:
                            ExecuteRangedMovement();
                            break;
                        case AIArchetype.Hybrid:
                            if (isReady && abilityToUse.range <= meleeAttackRange)
                                ExecuteClosingMovement(abilityToUse);
                            else
                                ExecuteRangedMovement();
                            break;
                    }
                }
            }
        }
        else
        {
            if (isTimid) { currentTarget = null; return; }
            timeSinceLostSight += Time.deltaTime;
            SetAIStatus("Combat", "Searching...");
            if (timeSinceLostSight > lostSightSearchDuration) currentTarget = null;
            else navMeshAgent.SetDestination(lastKnownPosition);
        }
    }

    private void ExecuteClosingMovement(Ability ability)
    {
        navMeshAgent.speed = originalSpeed;
        if (assignedSurroundPoint != null)
        {
            SurroundPointManager.instance.ReleasePoint(this);
            assignedSurroundPoint = null;
        }

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

    private void ExecuteMeleeMovement() { navMeshAgent.speed = originalSpeed; if (assignedSurroundPoint == null) assignedSurroundPoint = SurroundPointManager.instance.RequestPoint(this, currentTarget); Vector3 destination = assignedSurroundPoint != null ? assignedSurroundPoint.position : currentTarget.position; navMeshAgent.stoppingDistance = 0.5f; navMeshAgent.SetDestination(destination); SetAIStatus("Combat", assignedSurroundPoint != null ? "Circling" : "Waiting"); }
    private void UpdateIdleState() { currentTarget = targeting.FindBestTarget(); if (currentTarget != null) { hasUsedInitialAbilities = false; currentState = AIState.Combat; combatStartPosition = transform.position; if (navMeshAgent.hasPath) navMeshAgent.ResetPath(); return; } if (collectedPatrolPoints == null || collectedPatrolPoints.Length == 0) { SetAIStatus("Idle", "Searching..."); return; } if (isWaitingAtPatrolPoint) { SetAIStatus("Idle", "Waiting"); waitTimer -= Time.deltaTime; if (waitTimer <= 0) { isWaitingAtPatrolPoint = false; PatrolPoint lastPoint = collectedPatrolPoints[currentPatrolIndex]; if (lastPoint.nextPointOverride != null) { int nextIndex = Array.IndexOf(collectedPatrolPoints, lastPoint.nextPointOverride); currentPatrolIndex = (nextIndex >= 0) ? nextIndex : 0; } else if (lastPoint.jumpToRandomPoint) { currentPatrolIndex = UnityEngine.Random.Range(0, collectedPatrolPoints.Length); } else { currentPatrolIndex = (currentPatrolIndex + 1) % collectedPatrolPoints.Length; } navMeshAgent.SetDestination(collectedPatrolPoints[currentPatrolIndex].transform.position); } } else if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f) { PatrolPoint currentPoint = collectedPatrolPoints[currentPatrolIndex]; float waitTime = UnityEngine.Random.Range(currentPoint.minWaitTime, currentPoint.maxWaitTime); if (waitTime > 0) { isWaitingAtPatrolPoint = true; waitTimer = waitTime; } else { if (currentPoint.nextPointOverride != null) { int nextIndex = Array.IndexOf(collectedPatrolPoints, currentPoint.nextPointOverride); currentPatrolIndex = (nextIndex >= 0) ? nextIndex : 0; } else if (currentPoint.jumpToRandomPoint) { currentPatrolIndex = UnityEngine.Random.Range(0, collectedPatrolPoints.Length); } else { currentPatrolIndex = (currentPatrolIndex + 1) % collectedPatrolPoints.Length; } navMeshAgent.SetDestination(collectedPatrolPoints[currentPatrolIndex].transform.position); } } else { SetAIStatus("Idle", "Patrolling"); } }
    private void UpdateReturningState() { navMeshAgent.speed = originalSpeed; SetAIStatus("Returning", "Leashing"); currentTarget = targeting.FindBestTarget(); if (currentTarget != null) { currentState = AIState.Combat; combatStartPosition = transform.position; return; } if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance) { navMeshAgent.ResetPath(); currentState = AIState.Idle; } }
    private void UpdateRetreatingState() { retreatTimer -= Time.deltaTime; if (retreatTimer > 0 && currentTarget != null) { SetAIStatus("Combat", "Retreating"); navMeshAgent.speed = retreatAndKiteSpeed; RetreatFromTarget(); } else { navMeshAgent.speed = originalSpeed; currentState = AIState.Combat; } }
    private void ResetCombatState() { hasUsedInitialAbilities = false; if (assignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; } if (abilityHolder.ActiveBeam != null) abilityHolder.ActiveBeam.Interrupt(); currentTarget = null; }
    private void ExecuteRangedMovement() { float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position); if (distanceToTarget < minimumRangedAttackRange) { navMeshAgent.speed = retreatAndKiteSpeed; RetreatFromTarget(); } else { navMeshAgent.speed = originalSpeed; if (distanceToTarget > preferredCombatRange) { Vector3 destination = currentTarget.position - (currentTarget.position - transform.position).normalized * preferredCombatRange; navMeshAgent.SetDestination(destination); SetAIStatus("Combat", "Advancing"); } else { navMeshAgent.ResetPath(); SetAIStatus("Combat", "In Range"); } } }
    private void RetreatFromTarget() { if (currentTarget == null) return; Vector3 directionAwayFromTarget = (transform.position - currentTarget.position).normalized; float bestPathLength = 0; Vector3 bestRetreatPoint = Vector3.zero; bool foundRetreatPoint = false; int numSamples = 8; float retreatArc = 120f; for (int i = 0; i < numSamples; i++) { float angle = (i / (float)(numSamples - 1) - 0.5f) * retreatArc; Vector3 sampleDirection = Quaternion.Euler(0, angle, 0) * directionAwayFromTarget; Vector3 potentialDestination = transform.position + sampleDirection * kiteDistance; if (NavMesh.SamplePosition(potentialDestination, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) { NavMeshPath path = new NavMeshPath(); if (navMeshAgent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete) { float pathLength = 0f; for (int j = 1; j < path.corners.Length; j++) { pathLength += Vector3.Distance(path.corners[j - 1], path.corners[j]); } if (pathLength > bestPathLength) { bestPathLength = pathLength; bestRetreatPoint = hit.position; foundRetreatPoint = true; } } } } if (foundRetreatPoint) { navMeshAgent.SetDestination(bestRetreatPoint); } }
    private bool HasLineOfSight(Transform target) { if (target == null) return false; Vector3 origin = transform.position + Vector3.up; Vector3 targetPosition = target.position + Vector3.up; Vector3 direction = targetPosition - origin; if (Physics.Raycast(origin, direction.normalized, direction.magnitude, targeting.obstacleLayers)) return false; return true; }
    private void HandleHealthChanged() { if (health.currentHealth <= 0 && !isDead) { isDead = true; if (assignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; } navMeshAgent.enabled = false; this.enabled = false; return; } if (behaviorProfile != null && !isDead) { float currentHealthPercent = (float)health.currentHealth / health.maxHealth; foreach (var trigger in behaviorProfile.healthTriggers) { if (currentHealthPercent <= trigger.healthPercentage && !triggeredHealthPhases.Contains(trigger)) { triggeredHealthPhases.Add(trigger); GameObject targetForAbility = (currentTarget != null) ? currentTarget.gameObject : this.gameObject; abilityHolder.UseAbility(trigger.abilityToUse, targetForAbility); break; } } } }
    private void HandlePlayerAbilityUsed(PlayerAbilityHolder player, Ability usedAbility) { PlayerMovement playerMovement = player.GetComponentInParent<PlayerMovement>(); if (isDead || abilityHolder.IsCasting || playerMovement == null || playerMovement.TargetObject != this.gameObject) return; if (behaviorProfile != null) { foreach (var trigger in behaviorProfile.reactiveTriggers) { if (trigger.triggerType == usedAbility.abilityType) { if (UnityEngine.Random.value <= trigger.chanceToReact) { abilityHolder.UseAbility(trigger.reactionAbility, player.gameObject); break; } } } } }
    private void HandleCastStarted(string abilityName, float castDuration) { if (enemyHealthUI != null) enemyHealthUI.StartCast(abilityName, castDuration); SetAIStatus("Combat", "Casting"); if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); }
    private void HandleCastFinished() { if (enemyHealthUI != null) enemyHealthUI.StopCast(); }
    public void ActivateAI() { if (this.enabled) return; this.enabled = true; if (navMeshAgent != null) navMeshAgent.enabled = true; if (animator != null) animator.enabled = true; }
    public void DeactivateAI() { if (!this.enabled) return; if (navMeshAgent != null && navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); this.enabled = false; if (navMeshAgent != null) navMeshAgent.enabled = false; if (animator != null) animator.enabled = false; }

    // Interface Implementation
    public void ExecuteLeap(Vector3 destination, Action onLandAction) { movementHandler?.ExecuteLeap(destination, onLandAction); }
    public void ExecuteCharge(GameObject target, Ability chargeAbility) { if (abilitySelector.abilities.Contains(chargeAbility)) movementHandler?.ExecuteCharge(target, chargeAbility); }
    public void ExecuteTeleport(Vector3 destination) { if (navMeshAgent.isOnNavMesh) navMeshAgent.Warp(destination); else transform.position = destination; }

    private void SetAIStatus(string state, string action) { string newMessage = $"{state} :: {action}"; if (lastStatusMessage == newMessage) return; lastStatusMessage = newMessage; enemyHealthUI?.UpdateStatus(state, action); if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowAIStatus(action, transform.position + Vector3.up * 3.5f); }
    private void CollectPatrolPoints() { if (patrolPaths == null || patrolPaths.Length == 0) return; List<PatrolPoint> points = new List<PatrolPoint>(); foreach (PatrolPath path in patrolPaths) { if (path == null) continue; Collider pathCollider = path.GetComponent<Collider>(); if (pathCollider == null) continue; Collider[] collidersInVolume = Physics.OverlapBox(path.transform.position, pathCollider.bounds.extents, path.transform.rotation); foreach (Collider col in collidersInVolume) { if (col.TryGetComponent<PatrolPoint>(out PatrolPoint point)) { if (!points.Contains(point)) points.Add(point); } } } collectedPatrolPoints = points.OrderBy(p => p.gameObject.name).ToArray(); }
    void OnDestroy() { if (assignedSurroundPoint != null && SurroundPointManager.instance != null) { SurroundPointManager.instance.ReleasePoint(this); } if (health != null) health.OnHealthChanged -= HandleHealthChanged; if (abilityHolder != null) { abilityHolder.OnCastStarted -= HandleCastStarted; abilityHolder.OnCastFinished -= HandleCastFinished; } }
    void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, meleeAttackRange); Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, preferredCombatRange); Vector3 leashCenter = Application.isPlaying ? (currentState == AIState.Combat ? combatStartPosition : startPosition) : transform.position; Gizmos.color = Color.blue; Gizmos.DrawWireSphere(leashCenter, chaseLeashRadius); }
}