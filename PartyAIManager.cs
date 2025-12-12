using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
using UnityEngine.AI;

public class PartyAIManager : MonoBehaviour
{
    public static PartyAIManager instance;
    public static event Action<GameObject, AIStance> OnStanceChanged;
    public event Action<GameObject> OnAllyNeedsHealing;

    [Header("Core References")]
    public PartyManager partyManager;

    [Header("Formation Settings")]
    public Vector3[] followOffsets;

    public List<PartyMemberAI> AllPartyAIs { get; private set; } = new List<PartyMemberAI>();
    private Dictionary<PartyMemberAI, AIStance> stanceAssignments = new Dictionary<PartyMemberAI, AIStance>();
    private Dictionary<PartyMemberAI, Vector3> formationSlots = new Dictionary<PartyMemberAI, Vector3>();
    private PlayerMovement activePlayerController;

    public GameObject ActivePlayer => (partyManager != null) ? partyManager.ActivePlayer : null;
    private GameObject partyFocusTarget;
    public GameObject GetPartyFocusTarget() => partyFocusTarget;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    void OnEnable()
    {
        PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;
        StartCoroutine(PopulateAIsAfterFrame());
    }

    void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
    }

    // --- METHOD WITH THE FIX ---
    public void EnterTownMode()
    {
        partyFocusTarget = null;
        // --- FIX: Replaced FindObjectsOfType with FindObjectsByType ---
        TownCharacterSpawnPoint[] spawnPoints = FindObjectsByType<TownCharacterSpawnPoint>(FindObjectsSortMode.None);

        for (int i = 0; i < partyManager.partyMembers.Count; i++)
        {
            GameObject member = partyManager.partyMembers[i];
            if (member == null) continue;

            NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
            PlayerMovement movement = member.GetComponent<PlayerMovement>();
            PartyMemberAI ai = member.GetComponent<PartyMemberAI>();

            if (i == 0) // This is the active player
            {
                // --- ADDED LINE ---
                // Ensure the leader's GameObject is active before enabling components.
                member.SetActive(true);

                if (agent != null) agent.enabled = true;
                if (movement != null) movement.enabled = true;
                if (ai != null) ai.enabled = false;
            }
            else // These are the benched NUCs
            {
                // We can set these to active=true before warping to ensure components are ready.
                member.SetActive(true);
                TownCharacterSpawnPoint point = spawnPoints.FirstOrDefault(p => p.partyMemberIndex == i);
                if (point != null && agent != null)
                {
                    agent.enabled = true;
                    agent.Warp(point.transform.position);
                }

                if (movement != null) movement.enabled = false;
                if (ai != null) ai.enabled = false;
            }
        }

        HandleActivePlayerChanged(partyManager.ActivePlayer);
    }

    public void EnterCombatMode()
    {
        for (int i = 0; i < partyManager.partyMembers.Count; i++)
        {
            GameObject member = partyManager.partyMembers[i];
            if (member == null) continue;

            NavMeshAgent agent = member.GetComponent<NavMeshAgent>();

            if (i == 0)
            {
                member.SetActive(false);
                if (agent != null) agent.enabled = false;
            }
            else
            {
                member.SetActive(true);
                if (agent != null) agent.enabled = true;
            }
        }

        if (partyManager != null)
        {
            partyManager.PrepareForCombatScene();
        }

        if (partyManager != null && partyManager.ActivePlayer != null)
        {
            HandleActivePlayerChanged(partyManager.ActivePlayer);
        }
    }

    private void HandleActivePlayerChanged(GameObject newActivePlayer)
    {
        if (newActivePlayer == null) return;

        bool switchingEnabled = partyManager.playerSwitchingEnabled;

        activePlayerController = newActivePlayer.GetComponent<PlayerMovement>();
        AssignFormationSlots();

        foreach (var ai in AllPartyAIs)
        {
            if (ai == null) continue;

            PlayerMovement movement = ai.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.enabled = (ai.gameObject == newActivePlayer);
            }

            PartyMemberAI aiComponent = ai.GetComponent<PartyMemberAI>();
            if (aiComponent != null)
            {
                aiComponent.enabled = (ai.gameObject != newActivePlayer) && switchingEnabled;
            }
        }
    }

    public void SetStanceForCharacter(GameObject character, AIStance newStance)
    {
        if (character.TryGetComponent<PartyMemberAI>(out var ai))
        {
            stanceAssignments[ai] = newStance;
            ai.currentStance = newStance;
            OnStanceChanged?.Invoke(character, newStance);
        }
    }

    #region Unchanged Code
    private IEnumerator PopulateAIsAfterFrame() { yield return null; AllPartyAIs = GetComponentsInChildren<PartyMemberAI>(true).ToList(); foreach (var ai in AllPartyAIs) { stanceAssignments[ai] = ai.currentStance; } AssignFormationSlots(); }
    public void IssueCommandAttack(GameObject target) { partyFocusTarget = target; foreach (var ai in AllPartyAIs) { if (ai != null && ai.enabled && ai.gameObject.activeInHierarchy && ai.gameObject != ActivePlayer) { ai.SetCommand(AICommand.AttackTarget, target); } } }
    public void IssueCommandMoveTo(Vector3 position) { partyFocusTarget = null; int offsetIndex = 0; foreach (var ai in AllPartyAIs) { if (ai != null && ai.enabled && ai.gameObject.activeInHierarchy && ai.gameObject != ActivePlayer) { Vector3 offset = Vector3.zero; if (offsetIndex < followOffsets.Length) { offset = followOffsets[offsetIndex]; offsetIndex++; } Vector3 individualPosition = position + offset; ai.SetCommand(AICommand.MoveToAndDefend, null, individualPosition); } } }
    public void IssueCommandFollow() { partyFocusTarget = null; foreach (var ai in AllPartyAIs) { if (ai != null && ai.enabled && ai.gameObject.activeInHierarchy) { ai.SetCommand(AICommand.Follow); } } }
    private void AssignFormationSlots() { if (ActivePlayer == null) return; int slotIndex = 0; foreach (var ai in AllPartyAIs) { if (ai.gameObject != ActivePlayer) { if (slotIndex < followOffsets.Length) { formationSlots[ai] = followOffsets[slotIndex]; slotIndex++; } } } }
    public Vector3 GetFormationPositionFor(PartyMemberAI ai) { if (activePlayerController != null) { if (formationSlots.TryGetValue(ai, out Vector3 offset)) { return activePlayerController.transform.TransformPoint(offset); } } return transform.position; }
    public void IssueCommandAttackSingle(GameObject nuc, GameObject target) { if (nuc.TryGetComponent<PartyMemberAI>(out var ai) && ai.enabled) { ai.SetCommand(AICommand.AttackTarget, target); } }
    public void IssueCommandMoveToSingle(GameObject nuc, Vector3 position) { if (nuc.TryGetComponent<PartyMemberAI>(out var ai) && ai.enabled) { ai.SetCommand(AICommand.MoveToAndDefend, null, position); } }
    public void CallForHelp(GameObject allyInNeed) { OnAllyNeedsHealing?.Invoke(allyInNeed); }
    public AIStance GetStanceForCharacter(GameObject character) { if (character.TryGetComponent<PartyMemberAI>(out var ai) && stanceAssignments.TryGetValue(ai, out var stance)) { return stance; } return AIStance.Defensive; }
    #endregion
}