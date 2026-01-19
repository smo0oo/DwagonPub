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
    // --- Public Properties ---
    public NavMeshAgent NavAgent { get; private set; }
    public Health Health { get; private set; }
    public EnemyAbilityHolder AbilityHolder { get; private set; }
    public AITargeting Targeting { get; private set; }
    public AIAbilitySelector AbilitySelector { get; private set; }
    public Animator Animator { get; private set; }
    public CharacterMovementHandler MovementHandler { get; private set; }

    // --- State Data ---
    // FIX: Added [HideInInspector] to prevent UnassignedReferenceException since this is runtime-only
    [HideInInspector]
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

    // --- Cooldown Tracker for Retreating ---
    public float LastRetreatTime { get; set; } = -999f;
    public float RetreatCooldown { get; set; } = 10f; // Don't try to flee again for 10s
    // -------------------------------------------

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

    // --- State Machine ---
    private string lastState = "";
    private string lastAction = "";
    private IEnemyState currentState;

    // --- Animation Hashes (2D Movement) ---
    private int velocityXHash;
    private int velocityZHash;
    private int attackTriggerHash;
    private int attackIndexHash;
    private int idleIndexHash;
    private int walkIndexHash;
    private bool wasMoving = false;

    // --- Caching ---
    private Transform _cachedPlayerTransform;

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

        // --- 2D Blend Tree Hashes ---
        velocityXHash = Animator.StringToHash("VelocityX");
        velocityZHash = Animator.StringToHash("VelocityZ");

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

        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.2f));
        WaitForSeconds wait = new WaitForSeconds(0.1f);

        while (!isDead && this.enabled)
        {
            if (NavAgent != null && NavAgent.isOnNavMesh && NavAgent.isActiveAndEnabled)
            {
                if (Time.time >= recoveryTimer) currentState?.Execute(this);
            }
            yield return wait;
        }
    }

    void Update()
    {
        if (isDead) return;

        HandleRotation();

        // 1. Animation Lock Check
        bool isAnimationLocked = false;
        if (Animator != null)
        {
            AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsTag("Attack") || (Animator.IsInTransition(0) && Animator.GetAnimatorTransitionInfo(0).anyState && stateInfo.IsTag("Attack")))
            {
                isAnimationLocked = true;
            }
        }

        if (isAnimationLocked || Time.time < recoveryTimer)
        {
            StopMovement();
            // Zero out motion when locked
            if (Animator != null)
            {
                Animator.SetFloat(velocityXHash, 0f);
                Animator.SetFloat(velocityZHash, 0f);
            }
            return;
        }

        UpdateAnimator();

        if (IsInActionSequence || (MovementHandler != null && MovementHandler.IsSpecialMovementActive) || AbilityHolder.IsCasting || AbilityHolder.ActiveBeam != null)
        {
            if (AbilityHolder.IsCasting) StopMovement();
            if (AbilityHolder.ActiveBeam != null && currentTarget != null)
            {
                Vector3 lookDir = currentTarget.position - transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
            }
            return;
        }
    }

    // --- 2D Animation Logic ---
    private void UpdateAnimator()
    {
        if (Animator == null) return;
        if (NavAgent == null || !NavAgent.isActiveAndEnabled || !NavAgent.isOnNavMesh)
        {
            Animator.SetFloat(velocityXHash, 0f);
            Animator.SetFloat(velocityZHash, 0f);
            return;
        }

        // Get World Velocity
        Vector3 worldVelocity = NavAgent.velocity;

        // Handle "Sliding" when near destination (Agent slows down but velocity stays high briefly)
        if (NavAgent.remainingDistance < 0.1f && !NavAgent.isStopped) worldVelocity = Vector3.zero;

        // Convert to Local Space (Relative to facing)
        // Z = Forward/Back, X = Right/Left
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        float speed = NavAgent.speed > 0 ? NavAgent.speed : 3.5f;

        // Normalize (-1 to 1)
        float vX = localVelocity.x / speed;
        float vZ = localVelocity.z / speed;

        // Update Animator
        Animator.SetFloat(velocityXHash, vX, 0.1f, Time.deltaTime);
        Animator.SetFloat(velocityZHash, vZ, 0.1f, Time.deltaTime);

        bool isMoving = worldVelocity.sqrMagnitude > 0.05f;
        if (isMoving != wasMoving)
        {
            if (isMoving) Animator.SetInteger(walkIndexHash, UnityEngine.Random.Range(0, 2));
            else Animator.SetInteger(idleIndexHash, UnityEngine.Random.Range(0, 3));
            wasMoving = isMoving;
        }
    }

    public void PerformAttack(Ability ability)
    {
        if (Animator != null)
        {
            if (!string.IsNullOrEmpty(ability.overrideTriggerName))
            {
                Animator.SetTrigger(ability.overrideTriggerName);
            }
            else
            {
                Animator.SetInteger(attackIndexHash, ability.attackStyleIndex > 0 ? ability.attackStyleIndex : UnityEngine.Random.Range(0, 3));
                Animator.SetTrigger(attackTriggerHash);
            }
        }
        AbilityHolder.UseAbility(ability, currentTarget?.gameObject ?? gameObject);
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
        if (NavAgent == null || !NavAgent.isActiveAndEnabled || !NavAgent.isOnNavMesh || currentTarget == null) return;

        Vector3 directionAway = (transform.position - currentTarget.position).normalized;
        Vector3 bestPoint = transform.position;
        bool found = false;

        // Kiting Logic: Look for a spot behind us
        for (int i = 0; i < 8; i++)
        {
            float angle = (i / 7f - 0.5f) * 120f;
            Vector3 sampleDir = Quaternion.Euler(0, angle, 0) * directionAway;
            Vector3 samplePos = transform.position + sampleDir * kiteDistance;

            if (NavMesh.SamplePosition(samplePos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                if (NavAgent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    bestPoint = hit.position;
                    found = true;
                    break;
                }
            }
        }
        if (found) NavAgent.SetDestination(bestPoint);
    }

    public bool IsTargetInvalid(Transform target)
    {
        if (target == null) return true;
        Health h = target.GetComponent<CharacterRoot>()?.Health ?? target.GetComponent<Health>();
        return h == null || h.isDowned || h.currentHealth <= 0;
    }

    public bool HasLineOfSight(Transform target)
    {
        if (Targeting != null && !Targeting.checkLineOfSight) return true;
        if (target == null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 dest = target.position + Vector3.up;
        if (Physics.Raycast(origin, (dest - origin).normalized, out RaycastHit hit, Vector3.Distance(origin, dest), Targeting.obstacleLayers))
        {
            return hit.transform == target || hit.transform.IsChildOf(target);
        }
        return true;
    }

    public void ResetCombatState()
    {
        HasUsedInitialAbilities = false;
        if (AssignedSurroundPoint != null) { SurroundPointManager.instance?.ReleasePoint(this); AssignedSurroundPoint = null; }
        if (AbilityHolder.ActiveBeam != null) AbilityHolder.ActiveBeam.Interrupt();
        currentTarget = null;
    }

    public void SetAIStatus(string state, string action)
    {
        if (lastState == state && lastAction == action) return;
        lastState = state; lastAction = action;
        enemyHealthUI?.UpdateStatus(state, action);
        if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowAIStatus(action, transform.position + Vector3.up * 3.5f);
    }

    private Transform GetPlayerTransform()
    {
        if (_cachedPlayerTransform == null)
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) _cachedPlayerTransform = playerGO.transform;
        }
        return _cachedPlayerTransform;
    }

    private void HandleRotation()
    {
        if (NavAgent == null || isDead) return;

        Vector3 targetLookPos = Vector3.zero;
        bool shouldRotate = false;

        // Sequence Priority Logic
        if (IsInActionSequence)
        {
            if (currentTarget != null)
            {
                targetLookPos = currentTarget.position;
                shouldRotate = true;
            }
            else
            {
                Transform player = GetPlayerTransform();
                if (player != null)
                {
                    targetLookPos = player.position;
                    shouldRotate = true;
                }
            }
        }
        // Standard Logic
        else if (currentTarget != null)
        {
            targetLookPos = currentTarget.position;
            shouldRotate = true;
        }
        else if (NavAgent.velocity.sqrMagnitude > 0.1f)
        {
            targetLookPos = transform.position + NavAgent.velocity;
            shouldRotate = true;
        }

        if (shouldRotate)
        {
            Vector3 dir = (targetLookPos - transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
            }
        }
    }

    private void OnDeath()
    {
        if (isDead) return;
        isDead = true;

        if (AbilityHolder != null) AbilityHolder.CancelCast();

        StartCoroutine(HandleCorpseCleanup());
        StopMovement();
        if (NavAgent != null) NavAgent.enabled = false;

        foreach (var comp in GetComponents<MonoBehaviour>())
        {
            if (comp != this && !(comp is Health)) comp.enabled = false;
        }
        GetComponent<Collider>().enabled = false;
        if (enemyHealthUI != null) enemyHealthUI.gameObject.SetActive(false);

        if (Animator != null)
        {
            Animator.SetFloat(velocityXHash, 0f);
            Animator.SetFloat(velocityZHash, 0f);
            Animator.ResetTrigger(attackTriggerHash);
        }
        SetAIStatus("Dead", "");
        this.enabled = false;
    }

    private IEnumerator HandleCorpseCleanup() { yield return new WaitForSeconds(corpseDuration); Destroy(gameObject); }
    public void ActivateAI() { hasBeenActivated = true; this.enabled = true; if (NavAgent != null) NavAgent.enabled = true; if (Animator != null) Animator.enabled = true; }
    public void DeactivateAI() { if (currentTarget != null) return; this.enabled = false; if (NavAgent != null) NavAgent.enabled = false; if (Animator != null) Animator.enabled = false; }

    private void CollectPatrolPoints()
    {
        if (patrolPaths == null || patrolPaths.Length == 0) return;
        List<PatrolPoint> points = new List<PatrolPoint>();
        foreach (var path in patrolPaths)
        {
            if (path == null) continue;
            Collider col = path.GetComponent<Collider>();
            if (col == null) continue;
            var hits = Physics.OverlapBox(path.transform.position, col.bounds.extents, path.transform.rotation);
            foreach (var hit in hits) if (hit.TryGetComponent(out PatrolPoint p)) points.Add(p);
        }
        CollectedPatrolPoints = points.Distinct().OrderBy(p => p.gameObject.name).ToArray();
    }

    private void HandleHealthChanged()
    {
        if (Health.currentHealth <= 0 && !isDead) OnDeath();
        if (behaviorProfile != null && !isDead)
        {
            float hpPercent = (float)Health.currentHealth / Health.maxHealth;
            foreach (var trigger in behaviorProfile.healthTriggers)
            {
                if (hpPercent <= trigger.healthPercentage && !triggeredHealthPhases.Contains(trigger))
                {
                    triggeredHealthPhases.Add(trigger);

                    // FIX: Use explicit null check instead of '?.' to avoid Unity object lifetime issues + ensure fallback works correctly
                    GameObject targetObj = (currentTarget != null) ? currentTarget.gameObject : gameObject;
                    AbilityHolder.UseAbility(trigger.abilityToUse, targetObj);

                    break;
                }
            }
        }
    }

    private void HandlePlayerAbilityUsed(PlayerAbilityHolder player, Ability usedAbility)
    {
        if (isDead || AbilityHolder.IsCasting) return;
        var pMove = player.GetComponentInParent<PlayerMovement>();
        if (pMove == null || pMove.TargetObject != gameObject) return;

        if (behaviorProfile != null)
        {
            foreach (var trigger in behaviorProfile.reactiveTriggers)
            {
                if (trigger.triggerType == usedAbility.abilityType && UnityEngine.Random.value <= trigger.chanceToReact)
                {
                    AbilityHolder.UseAbility(trigger.reactionAbility, player.gameObject);
                    break;
                }
            }
        }
    }

    private void HandleCastStarted(string n, float d) { enemyHealthUI?.StartCast(n, d); SetAIStatus("Combat", "Casting"); if (NavAgent.isOnNavMesh) NavAgent.ResetPath(); }
    private void HandleCastFinished() { enemyHealthUI?.StopCast(); recoveryTimer = Time.time + attackRecoveryDelay; }
    public void ExecuteLeap(Vector3 d, Action a) => MovementHandler?.ExecuteLeap(d, a);
    public void ExecuteCharge(GameObject t, Ability c) { if (AbilitySelector.abilities.Contains(c)) MovementHandler?.ExecuteCharge(t, c); }
    public void ExecuteTeleport(Vector3 d) { if (NavAgent.isOnNavMesh) NavAgent.Warp(d); else transform.position = d; }

    void OnDestroy()
    {
        if (AssignedSurroundPoint != null) SurroundPointManager.instance?.ReleasePoint(this);
        PlayerAbilityHolder.OnPlayerAbilityUsed -= HandlePlayerAbilityUsed;
    }
}