using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PartyMemberTargeting), typeof(PartyMemberAbilitySelector))]
public class PartyMemberAI : MonoBehaviour
{
    public event Action<PartyMemberAI, string> OnStatusChanged;

    [Header("AI State")]
    public AICommand currentCommand = AICommand.Follow;
    public AIStance currentStance = AIStance.Defensive;

    [Header("Movement")]
    public float followStoppingDistance = 1.5f;
    public float rotationSpeed = 12f;

    [Header("Idle Behavior")]
    [Tooltip("If true, character will wander slightly when holding position.")]
    public bool enableIdleWander = true;
    [Tooltip("How far from their anchor point they can wander.")]
    public float wanderRadius = 3.0f;
    public float minWanderWait = 2.0f;
    public float maxWanderWait = 6.0f;
    private float idleTimer = 0f;

    [Header("Self Preservation")]
    [Tooltip("If health drops below this % (0-1), stop fighting and run to leader.")]
    [Range(0f, 1f)] public float retreatThreshold = 0.3f;
    private bool isRetreating = false;

    [Header("Animation")]
    public float animationDampTime = 0.1f;

    [Header("Behavior Thresholds")]
    [Range(0f, 1f)] public float finisherThreshold = 0.25f;

    [Header("Ranged Combat & Kiting")]
    public bool prefersToKeepDistance = false;
    public float preferredCombatDistance = 20f;
    public bool canKite = false;
    public float minimumCombatRange = 5f;
    [Range(0f, 1f)] public float chanceToKite = 0.5f;
    [Range(0.1f, 1f)] public float kitingSpeedMultiplier = 0.7f;

    private PartyMemberTargeting targeting;
    private PartyMemberAbilitySelector abilitySelector;
    private NavMeshAgent navMeshAgent;
    private PlayerAbilityHolder abilityHolder;
    private Health health;
    private PlayerStats playerStats;
    private Animator animator;
    private GameObject currentTarget;
    private bool hasExplicitCommand = false;
    private string characterName;
    private float originalNavMeshSpeed;
    private Vector3 commandMovePosition;

    // --- OPTIMIZATION VARIABLES ---
    private float lastPathUpdateTime = 0f;
    private const float PATH_UPDATE_INTERVAL = 0.25f; // Update path 4 times per second max
    // ------------------------------

    public string CurrentStatus { get; private set; }

    void Awake()
    {
        CharacterRoot root = GetComponent<CharacterRoot>();
        if (root != null)
        {
            navMeshAgent = root.GetComponent<NavMeshAgent>();
            abilityHolder = root.PlayerAbilityHolder;
            health = root.Health;
            playerStats = root.PlayerStats;
            animator = root.Animator;
            characterName = root.gameObject.name;
        }
        targeting = GetComponent<PartyMemberTargeting>();
        abilitySelector = GetComponent<PartyMemberAbilitySelector>();

        if (navMeshAgent != null)
        {
            originalNavMeshSpeed = navMeshAgent.speed;
            navMeshAgent.updateRotation = false; // Manual rotation handling
        }
    }

    void OnEnable()
    {
        StartCoroutine(InitializeAgent());
    }

    private IEnumerator InitializeAgent()
    {
        // Safety Check for NavMesh Binding
        if (navMeshAgent != null && !navMeshAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                navMeshAgent.Warp(hit.position);
            }
        }

        float timeout = 1.0f;
        while (navMeshAgent != null && !navMeshAgent.isOnNavMesh && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (navMeshAgent != null && !navMeshAgent.isOnNavMesh)
        {
            UpdateStatus("Stuck (Off NavMesh)");
            yield break;
        }

        if (navMeshAgent != null)
        {
            if (navMeshAgent.hasPath) navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
        hasExplicitCommand = false;
        UpdateStatus("Following");
        StartCoroutine(AIThinkRoutine());
    }

    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu) return;

        UpdateAnimator();

        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer == this.gameObject)
        {
            if (navMeshAgent != null && navMeshAgent.hasPath) navMeshAgent.ResetPath();
            return;
        }

        if (navMeshAgent == null || !navMeshAgent.isOnNavMesh || health == null || health.currentHealth <= 0) return;

        Act();
    }

    private IEnumerator AIThinkRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f);
        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.2f));

        while (true)
        {
            if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != this.gameObject
                && health != null && health.currentHealth > 0)
            {
                Think();
            }
            yield return wait;
        }
    }

    private void Think()
    {
        if (hasExplicitCommand)
        {
            if ((currentCommand == AICommand.AttackTarget || currentCommand == AICommand.HealTarget) &&
                (currentTarget == null || IsTargetDeadOrInvalid(currentTarget)))
            {
                hasExplicitCommand = false;
            }
            else
            {
                return;
            }
        }

        float hpPercent = health.currentHealth / (float)health.maxHealth;
        if (hpPercent < retreatThreshold)
        {
            isRetreating = true;
        }
        else if (hpPercent > retreatThreshold + 0.15f)
        {
            isRetreating = false;
        }

        if (isRetreating)
        {
            currentCommand = AICommand.Follow;
            currentTarget = null;
            UpdateStatus("Retreating (Low HP)");
            return;
        }

        if (currentStance == AIStance.Passive)
        {
            currentCommand = AICommand.Follow;
            currentTarget = null;
            UpdateStatus("Passive");
            return;
        }

        if (playerStats.characterClass.aiRole == PlayerClass.AILogicRole.Support)
        {
            PartyMemberAI allyToHeal = targeting.FindWoundedAlly();
            if (allyToHeal != null)
            {
                currentTarget = allyToHeal.gameObject;
                currentCommand = AICommand.HealTarget;
                return;
            }
        }

        GameObject enemyTarget = null;
        if (currentStance == AIStance.Defensive)
        {
            enemyTarget = PartyAIManager.instance.GetPartyFocusTarget();
        }
        else if (currentStance == AIStance.Aggressive)
        {
            enemyTarget = PartyAIManager.instance.GetPartyFocusTarget() ?? targeting.FindNearestEnemy();
        }

        if (enemyTarget != null)
        {
            currentTarget = enemyTarget;
            currentCommand = AICommand.AttackTarget;
        }
        else
        {
            currentTarget = null;
            currentCommand = AICommand.Follow;
        }
    }

    private void Act()
    {
        if (navMeshAgent == null || !navMeshAgent.isActiveAndEnabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        switch (currentCommand)
        {
            case AICommand.Follow: HandleFollowState(); break;
            case AICommand.AttackTarget: HandleAttackState(); break;
            case AICommand.HealTarget: HandleHealState(); break;
            case AICommand.MoveToAndDefend: HandleMoveToAndDefendState(); break;
        }
    }

    // --- UPDATED HELPER: Throttled Movement Logic ---
    private void MoveTo(Vector3 destination)
    {
        if (abilityHolder != null && (abilityHolder.IsCasting || abilityHolder.ActiveBeam != null))
        {
            abilityHolder.CancelCast();
        }

        if (navMeshAgent.isOnNavMesh)
        {
            if (Time.time > lastPathUpdateTime + PATH_UPDATE_INTERVAL || !navMeshAgent.hasPath)
            {
                navMeshAgent.SetDestination(destination);
                lastPathUpdateTime = Time.time;
            }
        }
    }
    // ------------------------------------------------

    private void HandleFollowState()
    {
        Vector3 formationPos = PartyAIManager.instance.GetFormationPositionFor(this);
        float distToFormation = Vector3.Distance(transform.position, formationPos);

        // --- FIX: Relax the leash calculation ---
        // If wandering is enabled, we only force them back if they wander BEYOND the allowed radius.
        // Otherwise, standard stopping distance logic applies.
        float leashThreshold = (enableIdleWander && !isRetreating) ? wanderRadius + 1.0f : followStoppingDistance + 0.5f;

        if (distToFormation > leashThreshold || isRetreating)
        {
            navMeshAgent.speed = originalNavMeshSpeed;
            navMeshAgent.stoppingDistance = (isRetreating || currentStance == AIStance.Passive) ? 1.0f : followStoppingDistance;

            MoveTo(formationPos);
            HandleSmoothRotation();

            if (!isRetreating && currentStance != AIStance.Passive)
                UpdateStatus("Following");
        }
        else if (enableIdleWander && !isRetreating)
        {
            // Wander around formation spot
            HandleIdleWander(formationPos);
        }
        else
        {
            if (!navMeshAgent.isStopped && navMeshAgent.hasPath) navMeshAgent.ResetPath();
            HandleSmoothRotation();
            UpdateStatus("Waiting");
        }
    }

    private void HandleMoveToAndDefendState()
    {
        float distToCommand = Vector3.Distance(transform.position, commandMovePosition);

        // --- FIX: Relax the leash for command position as well ---
        float commandLeash = enableIdleWander ? wanderRadius + 1.0f : 1.0f; // Default 1.0f tolerance if wandering off

        if (distToCommand > commandLeash)
        {
            navMeshAgent.speed = originalNavMeshSpeed;
            navMeshAgent.stoppingDistance = 1.0f; // Ensure they actually stop near the point
            MoveTo(commandMovePosition);
            UpdateStatus("Moving to Position");
            HandleSmoothRotation();
        }
        else if (enableIdleWander)
        {
            HandleIdleWander(commandMovePosition);
        }
        else
        {
            HandleSmoothRotation();
            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                UpdateStatus("Holding Position");
            }
        }
    }

    private void HandleIdleWander(Vector3 anchorPosition)
    {
        // If moving, just let them finish
        if (navMeshAgent.hasPath && navMeshAgent.remainingDistance > 0.5f)
        {
            HandleSmoothRotation();
            UpdateStatus("Idling");
            return;
        }

        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
        {
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * wanderRadius;
            randomOffset.y = 0;
            Vector3 potentialTarget = anchorPosition + randomOffset;

            if (NavMesh.SamplePosition(potentialTarget, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                navMeshAgent.speed = originalNavMeshSpeed * 0.5f; // Walk slow

                // --- CRITICAL FIX: Reduce stopping distance ---
                // Otherwise they think they are "close enough" (because of the 1.5f buffer) and won't move.
                navMeshAgent.stoppingDistance = 0.1f;

                MoveTo(hit.position);
                idleTimer = UnityEngine.Random.Range(minWanderWait, maxWanderWait);
            }
        }
        else
        {
            HandleSmoothRotation();
            UpdateStatus("Idling");
        }
    }

    private void HandleAttackState()
    {
        if (currentTarget == null) { currentCommand = AICommand.Follow; return; }

        RotateTowards(currentTarget.transform.position);

        if (targeting.HasLineOfSight(currentTarget.transform))
        {
            navMeshAgent.speed = originalNavMeshSpeed;

            Ability abilityToUse = abilitySelector.SelectBestAbility(currentTarget);
            if (abilityToUse == null) return;

            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);

            if (distanceToTarget > abilityToUse.range)
            {
                navMeshAgent.stoppingDistance = abilityToUse.range * 0.8f; // Ensure we stop in range
                MoveTo(currentTarget.transform.position);
                UpdateStatus("Closing In");
            }
            else
            {
                navMeshAgent.ResetPath();
                abilityHolder.UseAbility(abilityToUse, currentTarget);
                UpdateStatus($"Attacking: {currentTarget.name}");
            }
        }
        else
        {
            MoveTo(currentTarget.transform.position);
            UpdateStatus("Repositioning");
        }
    }

    private void HandleHealState()
    {
        if (currentTarget == null) { currentCommand = AICommand.Follow; return; }

        RotateTowards(currentTarget.transform.position);

        if (targeting.HasLineOfSight(currentTarget.transform))
        {
            navMeshAgent.speed = originalNavMeshSpeed;

            Ability healAbility = abilitySelector.SelectBestAbility(currentTarget);
            if (healAbility == null) return;

            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget > healAbility.range)
            {
                navMeshAgent.stoppingDistance = healAbility.range * 0.8f;
                MoveTo(currentTarget.transform.position);
                UpdateStatus($"Moving to Heal");
            }
            else
            {
                navMeshAgent.ResetPath();
                abilityHolder.UseAbility(healAbility, currentTarget);
                UpdateStatus($"Healing: {currentTarget.name}");
            }
        }
        else
        {
            MoveTo(currentTarget.transform.position);
            UpdateStatus("Repositioning to Heal");
        }
    }

    private void HandleSmoothRotation()
    {
        if (navMeshAgent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(navMeshAgent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
        else if (!hasExplicitCommand && PartyAIManager.instance.ActivePlayer != null)
        {
            // Optional: Face leader if idle
            /*
            Vector3 dirToPlayer = (PartyAIManager.instance.ActivePlayer.transform.position - transform.position).normalized;
            if (dirToPlayer != Vector3.zero) {
                Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
            }
            */
        }
    }

    private void RotateTowards(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private bool IsTargetDeadOrInvalid(GameObject target)
    {
        if (target == null) return true;
        Health h = target.GetComponent<Health>();
        return (h != null && (h.isDowned || h.currentHealth <= 0));
    }

    private void UpdateStatus(string newStatus)
    {
        if (CurrentStatus != newStatus)
        {
            CurrentStatus = newStatus;
            OnStatusChanged?.Invoke(this, CurrentStatus);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null || navMeshAgent == null) return;
        Vector3 localVelocity = transform.InverseTransformDirection(navMeshAgent.velocity);
        animator.SetFloat("VelocityZ", localVelocity.z / originalNavMeshSpeed, animationDampTime, Time.deltaTime);
        animator.SetFloat("VelocityX", localVelocity.x / originalNavMeshSpeed, animationDampTime, Time.deltaTime);
    }

    public void SetCommand(AICommand newCommand, GameObject target = null, Vector3 position = default)
    {
        hasExplicitCommand = (newCommand != AICommand.Follow);
        currentCommand = newCommand;
        currentTarget = target;
        commandMovePosition = position;
        Act();
    }
}