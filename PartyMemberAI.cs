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
    // --- NEW VARIABLE ---
    [Tooltip("How quickly the character turns to face their movement direction.")]
    public float rotationSpeed = 10f;
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
    // This field is unused and was causing the warning. It can be safely removed.
    // private bool isAtDefendPosition; 
    public string CurrentStatus { get; private set; }
    private Vector3 commandMovePosition;

    void Awake()
    {
        CharacterRoot root = GetComponent<CharacterRoot>();
        if (root != null) { navMeshAgent = root.GetComponent<NavMeshAgent>(); abilityHolder = root.PlayerAbilityHolder; health = root.Health; playerStats = root.PlayerStats; animator = root.Animator; characterName = root.gameObject.name; }
        targeting = GetComponent<PartyMemberTargeting>();
        abilitySelector = GetComponent<PartyMemberAbilitySelector>();
        if (navMeshAgent != null) { originalNavMeshSpeed = navMeshAgent.speed; }

        // Ensure rotation is always manually controlled for consistency with the player.
        if (navMeshAgent != null) { navMeshAgent.updateRotation = false; }
    }

    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu) { return; }
        UpdateAnimator();
        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer == this.gameObject) { if (navMeshAgent.hasPath) navMeshAgent.ResetPath(); return; }
        if (navMeshAgent == null || !navMeshAgent.isOnNavMesh || health == null || health.currentHealth <= 0) { return; }
        Think();
        Act();
    }

    // --- METHOD WITH THE PRIMARY FIX ---
    private void HandleFollowState()
    {
        navMeshAgent.speed = originalNavMeshSpeed;
        Vector3 destination = PartyAIManager.instance.GetFormationPositionFor(this);
        navMeshAgent.stoppingDistance = followStoppingDistance;

        if (Vector3.Distance(navMeshAgent.destination, destination) > 0.5f)
        {
            navMeshAgent.SetDestination(destination);
        }

        // --- NEW ROTATION LOGIC ---
        // If the agent is moving...
        if (navMeshAgent.velocity.sqrMagnitude > 0.1f)
        {
            // ...smoothly rotate to face the direction of movement.
            Quaternion lookRotation = Quaternion.LookRotation(navMeshAgent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
            UpdateStatus("Following");
        }
        // If the agent has stopped...
        else
        {
            // ...smoothly rotate to match the leader's orientation.
            GameObject activePlayer = PartyAIManager.instance.ActivePlayer;
            if (activePlayer != null)
            {
                Quaternion targetRotation = activePlayer.transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
            UpdateStatus("Following (Idle)");
        }
    }

    #region Unchanged Code
    private void UpdateStatus(string newStatus) { if (CurrentStatus != newStatus) { CurrentStatus = newStatus; OnStatusChanged?.Invoke(this, CurrentStatus); } }
    private void UpdateAnimator() { if (animator == null || navMeshAgent == null) return; Vector3 localVelocity = transform.InverseTransformDirection(navMeshAgent.velocity); animator.SetFloat("VelocityZ", localVelocity.z / navMeshAgent.speed, animationDampTime, Time.deltaTime); animator.SetFloat("VelocityX", localVelocity.x / navMeshAgent.speed, animationDampTime, Time.deltaTime); }
    private void Think() { if (hasExplicitCommand) { if ((currentCommand == AICommand.AttackTarget || currentCommand == AICommand.HealTarget) && (currentTarget == null || currentTarget.GetComponent<Health>()?.currentHealth <= 0)) { hasExplicitCommand = false; } return; } if (playerStats.characterClass.aiRole == PlayerClass.AILogicRole.Support) { PartyMemberAI allyToHeal = targeting.FindWoundedAlly(); if (allyToHeal != null) { currentTarget = allyToHeal.gameObject; currentCommand = AICommand.HealTarget; return; } } GameObject enemyTarget = null; if (currentStance == AIStance.Defensive) { enemyTarget = PartyAIManager.instance.GetPartyFocusTarget(); } else if (currentStance == AIStance.Aggressive) { enemyTarget = PartyAIManager.instance.GetPartyFocusTarget() ?? targeting.FindNearestEnemy(); } if (enemyTarget != null) { currentTarget = enemyTarget; currentCommand = AICommand.AttackTarget; } else { currentTarget = null; currentCommand = AICommand.Follow; } }
    private void Act() { switch (currentCommand) { case AICommand.Follow: HandleFollowState(); break; case AICommand.AttackTarget: HandleAttackState(); break; case AICommand.HealTarget: HandleHealState(); break; case AICommand.MoveToAndDefend: HandleMoveToAndDefendState(); break; } }
    private void HandleAttackState() { if (currentTarget == null) { currentCommand = AICommand.Follow; return; } transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(currentTarget.transform.position - transform.position), Time.deltaTime * rotationSpeed); if (targeting.HasLineOfSight(currentTarget.transform)) { Ability abilityToUse = abilitySelector.SelectBestAbility(currentTarget); if (abilityToUse == null) { return; } float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position); if (distanceToTarget > abilityToUse.range) { navMeshAgent.SetDestination(currentTarget.transform.position); UpdateStatus("Closing In"); } else { navMeshAgent.ResetPath(); abilityHolder.UseAbility(abilityToUse, currentTarget); UpdateStatus($"Attacking: {currentTarget.name}"); } } else { navMeshAgent.SetDestination(currentTarget.transform.position); UpdateStatus("Repositioning"); } }
    private void HandleHealState() { if (currentTarget == null) { currentCommand = AICommand.Follow; return; } transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(currentTarget.transform.position - transform.position), Time.deltaTime * rotationSpeed); if (targeting.HasLineOfSight(currentTarget.transform)) { Ability healAbility = abilitySelector.SelectBestAbility(currentTarget); if (healAbility == null) { return; } float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position); if (distanceToTarget > healAbility.range) { navMeshAgent.SetDestination(currentTarget.transform.position); UpdateStatus($"Moving to Heal"); } else { navMeshAgent.ResetPath(); abilityHolder.UseAbility(healAbility, currentTarget); UpdateStatus($"Healing: {currentTarget.name}"); } } else { navMeshAgent.SetDestination(currentTarget.transform.position); UpdateStatus("Repositioning to Heal"); } }
    void OnEnable() { StartCoroutine(InitializeAgent()); }
    private IEnumerator InitializeAgent() { while (navMeshAgent != null && !navMeshAgent.isOnNavMesh) { yield return null; } if (navMeshAgent != null) { if (navMeshAgent.hasPath) { navMeshAgent.ResetPath(); } navMeshAgent.isStopped = false; } hasExplicitCommand = false; UpdateStatus("Following"); }
    private void HandleMoveToAndDefendState() { navMeshAgent.SetDestination(commandMovePosition); UpdateStatus("Moving to Position"); if (navMeshAgent.velocity.sqrMagnitude > 0.1f) { Quaternion lookRotation = Quaternion.LookRotation(navMeshAgent.velocity.normalized); transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed); } if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance) { UpdateStatus("Holding Position"); } }
    public void SetCommand(AICommand newCommand, GameObject target = null, Vector3 position = default) { hasExplicitCommand = (newCommand != AICommand.Follow); currentCommand = newCommand; currentTarget = target; commandMovePosition = position; Act(); }
    #endregion
}