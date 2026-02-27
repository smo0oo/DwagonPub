using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics;

[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(EnemyAbilityHolder))]
[RequireComponent(typeof(AITargeting), typeof(AIAbilitySelector))]
public class EnemyAI : MonoBehaviour, IMovementHandler
{
    public NavMeshAgent NavAgent { get; private set; }
    public Health Health { get; private set; }
    public EnemyAbilityHolder AbilityHolder { get; private set; }
    public AITargeting Targeting { get; private set; }
    public AIAbilitySelector AbilitySelector { get; private set; }
    public Animator Animator { get; private set; }
    public CharacterMovementHandler MovementHandler { get; private set; }

    public StatusEffectHolder StatusEffects { get; private set; }

    public float LastExecutionTimeMs { get; private set; }
    private Stopwatch _perfWatch = new Stopwatch();

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

    public float LastRetreatTime { get; set; } = -999f;
    public float RetreatCooldown { get; set; } = 10f;

    [Header("Enemy Stats & Behavior")]
    public EnemyClass enemyClass;
    public AIBehaviorProfile behaviorProfile;

    [Header("Boss / Giant Settings")]
    public bool isStationary = false;

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

    private string lastState = "";
    private string lastAction = "";
    private IEnemyState currentState;

    private int velocityXHash;
    private int velocityZHash;
    private int attackTriggerHash;
    private int attackIndexHash;
    private int idleIndexHash;
    private int walkIndexHash;
    private int rollDodgeHash;
    private bool wasMoving = false;

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
        StatusEffects = GetComponent<StatusEffectHolder>();

        velocityXHash = Animator.StringToHash("VelocityX");
        velocityZHash = Animator.StringToHash("VelocityZ");
        attackTriggerHash = Animator.StringToHash("AttackTrigger");
        attackIndexHash = Animator.StringToHash("AttackIndex");
        idleIndexHash = Animator.StringToHash("IdleIndex");
        walkIndexHash = Animator.StringToHash("WalkIndex");
        rollDodgeHash = Animator.StringToHash("RollDodge");
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

        if (NavAgent != null)
        {
            NavAgent.updateRotation = false;

            if (isStationary)
            {
                NavAgent.speed = 0f;
                NavAgent.angularSpeed = 0f;
                NavAgent.stoppingDistance = 0f;
                NavAgent.avoidancePriority = 0;
            }
        }

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
            if (StatusEffects != null && StatusEffects.IsStunned)
            {
                yield return wait;
                continue;
            }

            if (NavAgent != null && NavAgent.isOnNavMesh && NavAgent.isActiveAndEnabled)
            {
                if (Time.time >= recoveryTimer) currentState?.Execute(this);
            }
            yield return wait;
        }
    }

    void Update()
    {
        _perfWatch.Restart();

        if (isDead)
        {
            _perfWatch.Stop();
            LastExecutionTimeMs = 0f;
            return;
        }

        if (StatusEffects != null)
        {
            if (StatusEffects.IsStunned)
            {
                StopMovement();
                if (Animator != null) Animator.speed = 0f;

                _perfWatch.Stop();
                LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
                return;
            }
            else
            {
                if (Animator != null && Animator.speed == 0f) Animator.speed = 1f;
                if (StatusEffects.IsRooted) StopMovement();
            }
        }

        HandleRotation();

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
            if (Animator != null && !IsInActionSequence)
            {
                Animator.SetFloat(velocityXHash, 0f);
                Animator.SetFloat(velocityZHash, 0f);
            }

            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        UpdateAnimator();

        if (IsInActionSequence || (MovementHandler != null && MovementHandler.IsSpecialMovementActive) || AbilityHolder.IsCasting || AbilityHolder.ActiveBeam != null)
        {
            if (AbilityHolder.IsCasting) StopMovement();
            if (AbilityHolder.ActiveBeam != null && currentTarget != null && (StatusEffects == null || !StatusEffects.IsStunned))
            {
                Vector3 lookDir = currentTarget.position - transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
            }

            _perfWatch.Stop();
            LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
            return;
        }

        _perfWatch.Stop();
        LastExecutionTimeMs = (float)_perfWatch.Elapsed.TotalMilliseconds;
    }

    private void UpdateAnimator()
    {
        if (Animator == null) return;
        if (IsInActionSequence) return;

        if (NavAgent == null || !NavAgent.isActiveAndEnabled || !NavAgent.isOnNavMesh || isStationary || (StatusEffects != null && StatusEffects.IsRooted))
        {
            Animator.SetFloat(velocityXHash, 0f);
            Animator.SetFloat(velocityZHash, 0f);
            return;
        }

        Vector3 worldVelocity = NavAgent.velocity;
        if (NavAgent.remainingDistance < 0.1f && !NavAgent.isStopped) worldVelocity = Vector3.zero;

        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        float speed = NavAgent.speed > 0 ? NavAgent.speed : 3.5f;

        float vX = localVelocity.x / speed;
        float vZ = localVelocity.z / speed;

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
        if (StatusEffects != null && StatusEffects.IsStunned) return;

        // Animation logic has been entirely shifted to EnemyAbilityHolder.cs
        // to match the AAA player pipeline and ensure casting perfectly syncs with execution.
        AbilityHolder.UseAbility(ability, currentTarget?.gameObject ?? gameObject);
    }

    public void StopMovement()
    {
        if (isStationary) return;

        if (NavAgent != null && NavAgent.isActiveAndEnabled && NavAgent.isOnNavMesh)
        {
            NavAgent.ResetPath();
            NavAgent.velocity = Vector3.zero;
        }
    }

    public void RetreatFromTarget()
    {
        if (isStationary || (StatusEffects != null && (StatusEffects.IsRooted || StatusEffects.IsStunned))) return;

        if (NavAgent == null || !NavAgent.isActiveAndEnabled || !NavAgent.isOnNavMesh || currentTarget == null) return;

        Vector3 directionAway = (transform.position - currentTarget.position).normalized;
        Vector3 bestPoint = transform.position;
        bool found = false;

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
        if (isStationary && AbilityHolder != null)
        {
            if (AbilityHolder.centerAnchor != null) origin = AbilityHolder.centerAnchor.position;
            else if (AbilityHolder.headAnchor != null) origin = AbilityHolder.headAnchor.position;
            else origin = transform.position + (Vector3.up * 5f);
        }

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
        if (StatusEffects != null && StatusEffects.IsStunned) return; // Prevent rotation while stunned

        Vector3 targetLookPos = Vector3.zero;
        bool shouldRotate = false;

        if (IsInActionSequence)
        {
            if (currentTarget != null) { targetLookPos = currentTarget.position; shouldRotate = true; }
            else
            {
                Transform player = GetPlayerTransform();
                if (player != null) { targetLookPos = player.position; shouldRotate = true; }
            }
        }
        else if (currentTarget != null) { targetLookPos = currentTarget.position; shouldRotate = true; }
        else if (NavAgent.velocity.sqrMagnitude > 0.1f && !isStationary)
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
            Animator.speed = 1f;
            Animator.SetFloat(velocityXHash, 0f);
            Animator.SetFloat(velocityZHash, 0f);
            Animator.ResetTrigger(attackTriggerHash);
        }
        SetAIStatus("Dead", "");
        this.enabled = false;
    }

    private IEnumerator HandleCorpseCleanup() { yield return new WaitForSeconds(corpseDuration); Destroy(gameObject); }
    public void ActivateAI() { hasBeenActivated = true; this.enabled = true; if (NavAgent != null && !isStationary) NavAgent.enabled = true; if (Animator != null) Animator.enabled = true; }
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

                    if (trigger.isPhaseTransition)
                    {
                        StartCoroutine(PerformPhaseTransition(trigger));
                    }
                    else
                    {
                        GameObject targetObj = (currentTarget != null) ? currentTarget.gameObject : gameObject;
                        AbilityHolder.UseAbility(trigger.abilityToUse, targetObj, true);
                    }
                    break;
                }
            }
        }
    }

    private IEnumerator PerformPhaseTransition(HealthThresholdTrigger trigger)
    {
        IsInActionSequence = true;

        if (Health != null) Health.isInvulnerable = true;
        if (StatusEffects != null && trigger.clearDebuffsOnPhase) StatusEffects.ClearAllNegativeEffects();

        if (Animator != null && !string.IsNullOrEmpty(trigger.phaseAnimationTrigger))
        {
            Animator.ResetTrigger(attackTriggerHash);
            Animator.SetTrigger(trigger.phaseAnimationTrigger);
        }

        StopMovement();

        if (trigger.abilityToUse != null)
        {
            GameObject targetObj = (currentTarget != null) ? currentTarget.gameObject : gameObject;
            AbilityHolder.UseAbility(trigger.abilityToUse, targetObj, true);
        }

        yield return new WaitForSeconds(trigger.invulnerabilityDuration);

        if (Health != null) Health.isInvulnerable = false;
        IsInActionSequence = false;
    }

    private void HandlePlayerAbilityUsed(PlayerAbilityHolder player, Ability usedAbility, GameObject target, Vector3 targetPosition)
    {
        if (isDead || AbilityHolder.IsCasting) return;

        bool isThreatToMe = false;

        if (target != null && (target == gameObject || target.transform.IsChildOf(this.transform)))
        {
            isThreatToMe = true;
        }
        else if (target == null)
        {
            float threatRadius = usedAbility.aoeRadius > 0 ? usedAbility.aoeRadius : 3f;
            if (Vector3.Distance(transform.position, targetPosition) <= threatRadius + 2f)
            {
                isThreatToMe = true;
            }
        }

        if (!isThreatToMe) return;

        if (behaviorProfile == null || behaviorProfile.reactiveTriggers == null || behaviorProfile.reactiveTriggers.Count == 0) return;

        foreach (var trigger in behaviorProfile.reactiveTriggers)
        {
            bool isMatch = false;

            if (trigger.specificAbilityTrigger != null) isMatch = (trigger.specificAbilityTrigger == usedAbility);
            else isMatch = (trigger.triggerType == usedAbility.abilityType);

            if (isMatch && UnityEngine.Random.value <= trigger.chanceToReact)
            {
                ExecuteReaction(trigger, player.transform);
                break;
            }
        }
    }

    private bool TryFindEvasionPoint(Vector3 threatPosition, float desiredDistance, out Vector3 safePosition)
    {
        safePosition = transform.position;
        Vector3 baseDirAway = (transform.position - threatPosition).normalized;
        baseDirAway.y = 0;

        float[] checkAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };

        foreach (float angle in checkAngles)
        {
            Vector3 rotatedDir = Quaternion.Euler(0, angle, 0) * baseDirAway;
            Vector3 testDestination = transform.position + (rotatedDir * desiredDistance);

            if (!UnityEngine.AI.NavMesh.Raycast(transform.position, testDestination, out UnityEngine.AI.NavMeshHit hit, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(testDestination, out UnityEngine.AI.NavMeshHit finalHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    safePosition = finalHit.position;
                    return true;
                }
            }
            else
            {
                if (hit.distance > (desiredDistance * 0.4f))
                {
                    Vector3 safeWallPos = hit.position - (rotatedDir * 0.5f);
                    if (UnityEngine.AI.NavMesh.SamplePosition(safeWallPos, out UnityEngine.AI.NavMeshHit finalHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        safePosition = finalHit.position;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void ExecuteReaction(ReactiveTrigger trigger, Transform playerTransform)
    {
        if (StatusEffects != null && (StatusEffects.IsStunned || StatusEffects.IsRooted))
        {
            UnityEngine.Debug.Log($"<color=grey>[Enemy Reaction] Blocked: Enemy is Stunned or Rooted.</color>");
            return;
        }

        switch (trigger.reactionAction)
        {
            case AIReactionAction.CastReactionAbility:
                if (trigger.reactionAbility != null)
                {
                    AbilityHolder.UseAbility(trigger.reactionAbility, playerTransform.gameObject);
                }
                break;

            case AIReactionAction.LeapBackward:
                if (isStationary || trigger.movementDistance <= 0.1f) return;

                if (MovementHandler != null && !MovementHandler.IsSpecialMovementActive)
                {
                    if (TryFindEvasionPoint(playerTransform.position, trigger.movementDistance, out Vector3 leapDest))
                    {
                        MovementHandler.ExecuteLeap(leapDest, null);
                    }
                }
                break;

            case AIReactionAction.RollAway:
                if (isStationary || trigger.movementDistance <= 0.1f) return;
                if (IsInActionSequence) return;

                if (TryFindEvasionPoint(playerTransform.position, trigger.movementDistance, out Vector3 rollDest))
                {
                    StartCoroutine(PerformRollDodge(rollDest));
                }
                break;

            case AIReactionAction.TeleportAway:
                if (isStationary) return;

                if (TryFindEvasionPoint(playerTransform.position, trigger.movementDistance, out Vector3 tpDest))
                {
                    ExecuteTeleport(tpDest);
                }
                break;
        }
    }

    private IEnumerator PerformRollDodge(Vector3 targetPos)
    {
        IsInActionSequence = true;

        if (Animator != null)
        {
            Animator.ResetTrigger(attackTriggerHash);
            Animator.Play("RollDodge", 0, 0f);
        }

        if (Health != null) Health.isInvulnerable = true;

        if (NavAgent != null && NavAgent.isOnNavMesh)
        {
            NavAgent.isStopped = true;
            NavAgent.updatePosition = false;
        }

        float rollDuration = 0.45f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < rollDuration)
        {
            if (isDead) yield break;

            elapsed += Time.deltaTime;

            float t = elapsed / rollDuration;
            float easeOut = 1f - (1f - t) * (1f - t);

            transform.position = Vector3.Lerp(startPos, targetPos, easeOut);
            yield return null;
        }

        if (!isDead)
        {
            transform.position = targetPos;

            if (NavAgent != null && NavAgent.isOnNavMesh)
            {
                NavAgent.nextPosition = transform.position;
                NavAgent.updatePosition = true;
                NavAgent.isStopped = false;
            }
        }

        if (Health != null) Health.isInvulnerable = false;
        IsInActionSequence = false;
    }

    private void HandleCastStarted(string n, float d) { enemyHealthUI?.StartCast(n, d); SetAIStatus("Combat", "Casting"); if (NavAgent.isOnNavMesh && !isStationary) NavAgent.ResetPath(); }
    private void HandleCastFinished() { enemyHealthUI?.StopCast(); recoveryTimer = Time.time + attackRecoveryDelay; }

    public void ExecuteLeap(Vector3 d, Action a) { if (!isStationary) MovementHandler?.ExecuteLeap(d, a); }
    public void ExecuteCharge(GameObject t, Ability c) { if (!isStationary && AbilitySelector.abilities.Contains(c)) MovementHandler?.ExecuteCharge(t, c); }
    public void ExecuteTeleport(Vector3 d) { if (!isStationary) { if (NavAgent.isOnNavMesh) NavAgent.Warp(d); else transform.position = d; } }

    void OnDestroy()
    {
        if (AssignedSurroundPoint != null) SurroundPointManager.instance?.ReleasePoint(this);
        PlayerAbilityHolder.OnPlayerAbilityUsed -= HandlePlayerAbilityUsed;
    }
}