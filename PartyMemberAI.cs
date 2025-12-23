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
        // Safety Check for NavMesh Binding:
        // If the agent is enabled but not bound to the NavMesh, attempt to find a valid spot.
        if (navMeshAgent != null && !navMeshAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                navMeshAgent.Warp(hit.position);
            }
        }

        // Wait up to 1 second for the agent to bind.
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

        // Safety: If this object is the active player, do not run AI logic
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
        // Random offset to prevent all AIs thinking on the exact same frame
        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.2f));

        while (true)
        {
            // Only think if NOT the active player
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
        // FIX: Safety Check. 
        // Prevents "GetRemainingDistance" error if Manager issues a command 
        // while this agent is disabled, off-mesh, or still initializing.
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

    private void HandleFollowState()
    {
        navMeshAgent.speed = originalNavMeshSpeed;
        Vector3 destination = PartyAIManager.instance.GetFormationPositionFor(this);
        navMeshAgent.stoppingDistance = (isRetreating || currentStance == AIStance.Passive) ? 1.0f : followStoppingDistance;

        if (Vector3.Distance(navMeshAgent.destination, destination) > 0.5f)
        {
            navMeshAgent.SetDestination(destination);
        }

        HandleSmoothRotation();

        if (!isRetreating && currentStance != AIStance.Passive)
            UpdateStatus("Following");
    }

    private void HandleAttackState()
    {
        if (currentTarget == null) { currentCommand = AICommand.Follow; return; }

        RotateTowards(currentTarget.transform.position);

        if (targeting.HasLineOfSight(currentTarget.transform))
        {
            Ability abilityToUse = abilitySelector.SelectBestAbility(currentTarget);
            if (abilityToUse == null) return;

            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);

            if (distanceToTarget > abilityToUse.range)
            {
                navMeshAgent.SetDestination(currentTarget.transform.position);
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
            navMeshAgent.SetDestination(currentTarget.transform.position);
            UpdateStatus("Repositioning");
        }
    }

    private void HandleHealState()
    {
        if (currentTarget == null) { currentCommand = AICommand.Follow; return; }

        RotateTowards(currentTarget.transform.position);

        if (targeting.HasLineOfSight(currentTarget.transform))
        {
            Ability healAbility = abilitySelector.SelectBestAbility(currentTarget);
            if (healAbility == null) return;

            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget > healAbility.range)
            {
                navMeshAgent.SetDestination(currentTarget.transform.position);
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
            navMeshAgent.SetDestination(currentTarget.transform.position);
            UpdateStatus("Repositioning to Heal");
        }
    }

    private void HandleMoveToAndDefendState()
    {
        navMeshAgent.SetDestination(commandMovePosition);
        UpdateStatus("Moving to Position");

        HandleSmoothRotation();

        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            UpdateStatus("Holding Position");
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
            // Idle: align with leader ONLY if just following
            Quaternion targetRotation = PartyAIManager.instance.ActivePlayer.transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
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
        animator.SetFloat("VelocityZ", localVelocity.z / navMeshAgent.speed, animationDampTime, Time.deltaTime);
        animator.SetFloat("VelocityX", localVelocity.x / navMeshAgent.speed, animationDampTime, Time.deltaTime);
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