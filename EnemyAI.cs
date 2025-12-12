using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;
using System.Linq;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(EnemyAbilityHolder))]
[RequireComponent(typeof(AITargeting), typeof(AIAbilitySelector))]
public class EnemyAI : MonoBehaviour, IMovementHandler
{
    // --- NEW STATE FLAG ---
    // This will be controlled by the SequenceEffect to pause the AI's main logic loop.
    public bool IsInActionSequence { get; set; } = false;

    [Header("Enemy Stats & Behavior")]
    public EnemyClass enemyClass;
    [Tooltip("Assign an AI Behavior Profile for boss-like mechanics.")]
    public AIBehaviorProfile behaviorProfile;

    private List<HealthThresholdTrigger> triggeredHealthPhases = new List<HealthThresholdTrigger>();

    // ...
    // The rest of your variables
    // ...

    void Update()
    {
        // --- MODIFIED GUARD CLAUSE ---
        // If the AI is in a sequence, dead, or performing another special move, pause its brain.
        bool isSpecialMovementActive = movementHandler != null && movementHandler.IsSpecialMovementActive;
        if (IsInActionSequence || isDead || isSpecialMovementActive || abilityHolder.IsCasting || abilityHolder.ActiveBeam != null)
        {
            if (abilityHolder.ActiveBeam != null && currentTarget != null)
            {
                transform.LookAt(new Vector3(currentTarget.position.x, transform.position.y, currentTarget.position.z));
                SetAIStatus("Combat", "Channeling");
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

    #region Full Unchanged Script
    private AITargeting targeting;
    private AIAbilitySelector abilitySelector;
    private CharacterMovementHandler movementHandler;
    [Header("Leashing & State")]
    public float chaseLeashRadius = 30f;
    public PatrolPath[] patrolPaths;
    [Header("Line of Sight")]
    [Tooltip("How many seconds the AI will search for a lost target before giving up.")]
    public float lostSightSearchDuration = 5f;
    [Header("Combat Style")]
    public AIArchetype archetype;
    public float meleeAttackRange = 3f;
    public float minimumRangedAttackRange = 5f;
    public float preferredCombatRange = 15f;
    public float kiteDistance = 20f;
    [Tooltip("The movement speed the AI uses when kiting or retreating.")]
    public float retreatAndKiteSpeed = 8f;
    [Tooltip("If true, this AI will disengage from combat as soon as it loses line of sight.")]
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
    void Awake() { navMeshAgent = GetComponent<NavMeshAgent>(); health = GetComponent<Health>(); abilityHolder = GetComponent<EnemyAbilityHolder>(); enemyHealthUI = GetComponentInChildren<EnemyHealthUI>(); animator = GetComponentInChildren<Animator>(); targeting = GetComponent<AITargeting>(); abilitySelector = GetComponent<AIAbilitySelector>(); movementHandler = GetComponent<CharacterMovementHandler>(); }
    void Start() { if (enemyClass != null) { if (health != null) { health.UpdateMaxHealth(enemyClass.maxHealth); health.SetToMaxHealth(); health.damageReductionPercent = enemyClass.damageMitigation; } } else { Debug.LogWarning($"Enemy '{gameObject.name}' has no EnemyClass assigned. It will use default stats.", this); } startPosition = transform.position; currentState = AIState.Idle; originalSpeed = navMeshAgent.speed; CollectPatrolPoints(); health.OnHealthChanged += HandleHealthChanged; if (abilityHolder != null) { abilityHolder.OnCastStarted += HandleCastStarted; abilityHolder.OnCastFinished += HandleCastFinished; } if (startDeactivated) { DeactivateAI(); } HandleHealthChanged(); }
    private void HandleHealthChanged() { if (health.currentHealth <= 0 && !isDead) { isDead = true; if (assignedSurroundPoint != null && SurroundPointManager.instance != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; } navMeshAgent.enabled = false; this.enabled = false; return; } if (behaviorProfile != null && !isDead) { float currentHealthPercent = (float)health.currentHealth / health.maxHealth; foreach (var trigger in behaviorProfile.healthTriggers) { if (currentHealthPercent <= trigger.healthPercentage && !triggeredHealthPhases.Contains(trigger)) { triggeredHealthPhases.Add(trigger); GameObject targetForAbility = (currentTarget != null) ? currentTarget.gameObject : this.gameObject; abilityHolder.UseAbility(trigger.abilityToUse, targetForAbility); Debug.Log($"{name} crossed health threshold {trigger.healthPercentage * 100}% and used {trigger.abilityToUse.name}!"); break; } } } }
    private void HandlePlayerAbilityUsed(PlayerAbilityHolder player, Ability usedAbility) { PlayerMovement playerMovement = player.GetComponentInParent<PlayerMovement>(); if (isDead || abilityHolder.IsCasting || playerMovement == null || playerMovement.TargetObject != this.gameObject) { return; } if (behaviorProfile != null) { foreach (var trigger in behaviorProfile.reactiveTriggers) { if (trigger.triggerType == usedAbility.abilityType) { if (UnityEngine.Random.value <= trigger.chanceToReact) { Debug.Log($"{name} is reacting to player's {usedAbility.name} by using {trigger.reactionAbility.name}!"); abilityHolder.UseAbility(trigger.reactionAbility, player.gameObject); break; } } } } }
    private void UpdateIdleState() { currentTarget = targeting.FindBestTarget(); if (currentTarget != null) { hasUsedInitialAbilities = false; currentState = AIState.Combat; combatStartPosition = transform.position; if (navMeshAgent.hasPath) { navMeshAgent.ResetPath(); } return; } if (collectedPatrolPoints == null || collectedPatrolPoints.Length == 0) { SetAIStatus("Idle", "Searching..."); return; } if (isWaitingAtPatrolPoint) { SetAIStatus("Idle", "Waiting"); waitTimer -= Time.deltaTime; if (waitTimer <= 0) { isWaitingAtPatrolPoint = false; PatrolPoint lastPoint = collectedPatrolPoints[currentPatrolIndex]; if (lastPoint.nextPointOverride != null) { int nextIndex = Array.IndexOf(collectedPatrolPoints, lastPoint.nextPointOverride); currentPatrolIndex = (nextIndex >= 0) ? nextIndex : 0; } else if (lastPoint.jumpToRandomPoint) { currentPatrolIndex = UnityEngine.Random.Range(0, collectedPatrolPoints.Length); } else { currentPatrolIndex = (currentPatrolIndex + 1) % collectedPatrolPoints.Length; } navMeshAgent.SetDestination(collectedPatrolPoints[currentPatrolIndex].transform.position); } } else if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f) { PatrolPoint currentPoint = collectedPatrolPoints[currentPatrolIndex]; float waitTime = UnityEngine.Random.Range(currentPoint.minWaitTime, currentPoint.maxWaitTime); if (waitTime > 0) { isWaitingAtPatrolPoint = true; waitTimer = waitTime; } else { if (currentPoint.nextPointOverride != null) { int nextIndex = Array.IndexOf(collectedPatrolPoints, currentPoint.nextPointOverride); currentPatrolIndex = (nextIndex >= 0) ? nextIndex : 0; } else if (currentPoint.jumpToRandomPoint) { currentPatrolIndex = UnityEngine.Random.Range(0, collectedPatrolPoints.Length); } else { currentPatrolIndex = (currentPatrolIndex + 1) % collectedPatrolPoints.Length; } navMeshAgent.SetDestination(collectedPatrolPoints[currentPatrolIndex].transform.position); } } else { SetAIStatus("Idle", "Patrolling"); } }
    private void UpdateCombatState() { if (currentTarget == null || Vector3.Distance(transform.position, combatStartPosition) > chaseLeashRadius) { hasUsedInitialAbilities = false; if (assignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; } if (abilityHolder.ActiveBeam != null) abilityHolder.ActiveBeam.Interrupt(); currentState = AIState.Returning; currentTarget = null; navMeshAgent.SetDestination(startPosition); return; } if (health.currentHealth / (float)health.maxHealth < retreatHealthThreshold) { if (UnityEngine.Random.value < retreatChance) { currentState = AIState.Retreating; retreatTimer = retreatDuration; if (assignedSurroundPoint != null) { SurroundPointManager.instance.ReleasePoint(this); assignedSurroundPoint = null; } return; } } if (HasLineOfSight(currentTarget)) { lastKnownPosition = currentTarget.position; timeSinceLostSight = 0f; transform.LookAt(new Vector3(currentTarget.position.x, transform.position.y, currentTarget.position.z)); Ability abilityToUse = abilitySelector.SelectBestAbility(currentTarget, hasUsedInitialAbilities); float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position); bool canUseAbility = abilityToUse != null && distanceToTarget <= abilityToUse.range; if (canUseAbility) { navMeshAgent.ResetPath(); SetAIStatus("Combat", $"Attacking: {abilityToUse.displayName}"); abilityHolder.UseAbility(abilityToUse, currentTarget.gameObject); if (abilitySelector.initialAbilities.Contains(abilityToUse)) { hasUsedInitialAbilities = true; } } else { bool isTargetDomeMarker = currentTarget.CompareTag("DomeMarker"); if (canUseAbility && abilityToUse.range <= meleeAttackRange && assignedSurroundPoint == null && !isTargetDomeMarker) { assignedSurroundPoint = SurroundPointManager.instance.RequestPoint(this, currentTarget); } switch (archetype) { case AIArchetype.Melee: if (isTargetDomeMarker) { ExecuteDirectMovement(); } else { ExecuteMeleeMovement(); } break; case AIArchetype.Ranged: ExecuteRangedMovement(); break; case AIArchetype.Hybrid: if (abilityToUse != null && abilityToUse.range <= meleeAttackRange) { if (isTargetDomeMarker) { ExecuteDirectMovement(); } else { ExecuteMeleeMovement(); } } else { ExecuteRangedMovement(); } break; } } } else { if (isTimid) { currentTarget = null; return; } timeSinceLostSight += Time.deltaTime; SetAIStatus("Combat", "Searching..."); if (timeSinceLostSight > lostSightSearchDuration) { currentTarget = null; } else { navMeshAgent.SetDestination(lastKnownPosition); } } }
    private void UpdateRetreatingState() { retreatTimer -= Time.deltaTime; if (retreatTimer > 0 && currentTarget != null) { SetAIStatus("Combat", "Retreating"); navMeshAgent.speed = retreatAndKiteSpeed; RetreatFromTarget(); } else { navMeshAgent.speed = originalSpeed; currentState = AIState.Combat; } }
    private void ExecuteRangedMovement() { float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position); if (distanceToTarget < minimumRangedAttackRange) { navMeshAgent.speed = retreatAndKiteSpeed; RetreatFromTarget(); } else { navMeshAgent.speed = originalSpeed; if (distanceToTarget > preferredCombatRange) { Vector3 destination = currentTarget.position - (currentTarget.position - transform.position).normalized * preferredCombatRange; navMeshAgent.SetDestination(destination); SetAIStatus("Combat", "Advancing"); } else { navMeshAgent.ResetPath(); SetAIStatus("Combat", "In Range"); } } }
    private void RetreatFromTarget() { if (currentTarget == null) return; Vector3 directionAwayFromTarget = (transform.position - currentTarget.position).normalized; float bestPathLength = 0; Vector3 bestRetreatPoint = Vector3.zero; bool foundRetreatPoint = false; int numSamples = 8; float retreatArc = 120f; for (int i = 0; i < numSamples; i++) { float angle = (i / (float)(numSamples - 1) - 0.5f) * retreatArc; Vector3 sampleDirection = Quaternion.Euler(0, angle, 0) * directionAwayFromTarget; Vector3 potentialDestination = transform.position + sampleDirection * kiteDistance; if (NavMesh.SamplePosition(potentialDestination, out NavMeshHit hit, 2.0f, NavMesh.AllAreas)) { NavMeshPath path = new NavMeshPath(); if (navMeshAgent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete) { float pathLength = 0f; for (int j = 1; j < path.corners.Length; j++) { pathLength += Vector3.Distance(path.corners[j - 1], path.corners[j]); } if (pathLength > bestPathLength) { bestPathLength = pathLength; bestRetreatPoint = hit.position; foundRetreatPoint = true; } } } } if (foundRetreatPoint) { navMeshAgent.SetDestination(bestRetreatPoint); } }
    private bool HasLineOfSight(Transform target) { if (target == null) return false; Vector3 origin = transform.position + Vector3.up; Vector3 targetPosition = target.position + Vector3.up; Vector3 direction = targetPosition - origin; if (Physics.Raycast(origin, direction.normalized, direction.magnitude, targeting.obstacleLayers)) { return false; } return true; }
    public void ExecuteLeap(Vector3 destination, Action onLandAction) { movementHandler?.ExecuteLeap(destination, onLandAction); }
    public void ExecuteCharge(GameObject target, Ability chargeAbility) { if (abilitySelector.abilities.Contains(chargeAbility)) { movementHandler?.ExecuteCharge(target, chargeAbility); } }
    public void ExecuteTeleport(Vector3 destination) { if (navMeshAgent.isOnNavMesh) navMeshAgent.Warp(destination); else transform.position = destination; }
    private void SetAIStatus(string state, string action) { string newMessage = $"{state} :: {action}"; if (lastStatusMessage == newMessage) return; lastStatusMessage = newMessage; enemyHealthUI?.UpdateStatus(state, action); if (FloatingTextManager.instance != null) { FloatingTextManager.instance.ShowAIStatus(action, transform.position + Vector3.up * 3.5f); } }
    private void ExecuteDirectMovement() { navMeshAgent.speed = originalSpeed; Ability ability = abilitySelector.SelectBestAbility(currentTarget, hasUsedInitialAbilities) ?? abilitySelector.abilities.FirstOrDefault(); float range = (ability != null) ? ability.range : meleeAttackRange; navMeshAgent.stoppingDistance = range * 0.9f; navMeshAgent.SetDestination(currentTarget.position); SetAIStatus("Combat", "Advancing on Dome"); }
    private void ExecuteMeleeMovement() { navMeshAgent.speed = originalSpeed; if (assignedSurroundPoint == null) { assignedSurroundPoint = SurroundPointManager.instance.RequestPoint(this, currentTarget); } Vector3 destination; if (assignedSurroundPoint != null) { destination = assignedSurroundPoint.position; } else { destination = currentTarget.position + (transform.position - currentTarget.position).normalized * waitingDistanceOffset; } navMeshAgent.stoppingDistance = 0.5f; navMeshAgent.SetDestination(destination); SetAIStatus("Combat", "Positioning"); }
    private void CollectPatrolPoints() { if (patrolPaths == null || patrolPaths.Length == 0) return; List<PatrolPoint> points = new List<PatrolPoint>(); foreach (PatrolPath path in patrolPaths) { if (path == null) continue; Collider pathCollider = path.GetComponent<Collider>(); if (pathCollider == null) continue; Collider[] collidersInVolume = Physics.OverlapBox(path.transform.position, pathCollider.bounds.extents, path.transform.rotation); foreach (Collider col in collidersInVolume) { if (col.TryGetComponent<PatrolPoint>(out PatrolPoint point)) { if (!points.Contains(point)) { points.Add(point); } } } } collectedPatrolPoints = points.OrderBy(p => p.gameObject.name).ToArray(); }
    void OnDestroy() { if (assignedSurroundPoint != null && SurroundPointManager.instance != null) { SurroundPointManager.instance.ReleasePoint(this); } if (health != null) health.OnHealthChanged -= HandleHealthChanged; if (abilityHolder != null) { abilityHolder.OnCastStarted -= HandleCastStarted; abilityHolder.OnCastFinished -= HandleCastFinished; } }
    private void HandleCastStarted(string abilityName, float castDuration) { if (enemyHealthUI != null) { enemyHealthUI.StartCast(abilityName, castDuration); } SetAIStatus("Combat", "Casting"); if (navMeshAgent.isOnNavMesh) { navMeshAgent.ResetPath(); } }
    private void HandleCastFinished() { if (enemyHealthUI != null) enemyHealthUI.StopCast(); }
    private void UpdateReturningState() { navMeshAgent.speed = originalSpeed; SetAIStatus("Returning", "Leashing"); currentTarget = targeting.FindBestTarget(); if (currentTarget != null) { currentState = AIState.Combat; combatStartPosition = transform.position; return; } if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance) { navMeshAgent.ResetPath(); currentState = AIState.Idle; } }
    void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, meleeAttackRange); Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, preferredCombatRange); Vector3 leashCenter = Application.isPlaying ? (currentState == AIState.Combat ? combatStartPosition : startPosition) : transform.position; Gizmos.color = Color.blue; Gizmos.DrawWireSphere(leashCenter, chaseLeashRadius); }
    public void ActivateAI() { if (this.enabled) return; this.enabled = true; if (navMeshAgent != null) navMeshAgent.enabled = true; if (animator != null) animator.enabled = true; }
    public void DeactivateAI() { if (!this.enabled) return; if (navMeshAgent != null && navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath(); this.enabled = false; if (navMeshAgent != null) navMeshAgent.enabled = false; if (animator != null) animator.enabled = false; }
    #endregion
}