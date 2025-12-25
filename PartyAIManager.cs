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

    public GameObject ActivePlayer => (partyManager != null) ? partyManager.ActivePlayer : null;
    private GameObject partyFocusTarget;
    public GameObject GetPartyFocusTarget() => partyFocusTarget;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    void Start()
    {
        StartCoroutine(InitialPopulate());
    }

    void OnEnable()
    {
        PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;
    }

    void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
    }

    private IEnumerator InitialPopulate()
    {
        yield return null;
        RefreshAIList();
        AssignFormationSlots();

        if (ActivePlayer != null)
        {
            HandleActivePlayerChanged(ActivePlayer);
        }
    }

    // FIX 2: Exposed method to force state update from GameManager explicitly
    public void ForceUpdateState(bool isTownMode)
    {
        RefreshAIList();
        AssignFormationSlots();

        foreach (var member in partyManager.partyMembers)
        {
            if (member == null) continue;
            bool isLeader = (member == ActivePlayer);

            bool shouldAIBeActive = !isLeader && !isTownMode;
            SetAIComponentsActive(member, shouldAIBeActive);
        }
    }

    public void EnterTownMode()
    {
        partyFocusTarget = null;
        TownCharacterSpawnPoint[] spawnPoints = FindObjectsByType<TownCharacterSpawnPoint>(FindObjectsSortMode.None);

        for (int i = 0; i < partyManager.partyMembers.Count; i++)
        {
            GameObject member = partyManager.partyMembers[i];
            if (member == null) continue;

            NavMeshAgent agent = member.GetComponent<NavMeshAgent>();

            if (i == 0) // Active Player
            {
                member.SetActive(true);
                if (agent != null) agent.enabled = true;
                SetAIComponentsActive(member, false);
            }
            else // Followers
            {
                member.SetActive(true);
                TownCharacterSpawnPoint point = spawnPoints.FirstOrDefault(p => p.partyMemberIndex == i);
                if (point != null && agent != null)
                {
                    agent.enabled = true;
                    agent.Warp(point.transform.position);
                }
                SetAIComponentsActive(member, false);
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

            if (i == 0) // Active Player
            {
                member.SetActive(false);
                if (agent != null) agent.enabled = false;
                SetAIComponentsActive(member, false);
            }
            else // Followers
            {
                member.SetActive(true);
                if (agent != null) agent.enabled = true;
                SetAIComponentsActive(member, true);
            }
        }

        if (partyManager != null)
        {
            partyManager.PrepareForCombatScene();
            HandleActivePlayerChanged(partyManager.ActivePlayer);
        }
    }

    private void HandleActivePlayerChanged(GameObject newActivePlayer)
    {
        if (partyManager == null) return;

        RefreshAIList();
        AssignFormationSlots();

        bool isTownMode = false;
        if (GameManager.instance != null)
        {
            isTownMode = (GameManager.instance.currentSceneType == SceneType.Town);
        }

        foreach (var member in partyManager.partyMembers)
        {
            if (member == null) continue;
            bool isLeader = (member == newActivePlayer);

            bool shouldAIBeActive = !isLeader && !isTownMode;
            SetAIComponentsActive(member, shouldAIBeActive);
        }
    }

    private void SetAIComponentsActive(GameObject root, bool isActive)
    {
        var ai = root.GetComponentInChildren<PartyMemberAI>(true);
        if (ai != null) ai.enabled = isActive;

        var targeting = root.GetComponentInChildren<PartyMemberTargeting>(true);
        if (targeting != null) targeting.enabled = isActive;

        var selector = root.GetComponentInChildren<PartyMemberAbilitySelector>(true);
        if (selector != null) selector.enabled = isActive;
    }

    private void RefreshAIList()
    {
        AllPartyAIs.Clear();
        if (partyManager != null)
        {
            foreach (var member in partyManager.partyMembers)
            {
                if (member != null)
                {
                    var ai = member.GetComponentInChildren<PartyMemberAI>(true);
                    if (ai != null)
                    {
                        AllPartyAIs.Add(ai);
                        if (!stanceAssignments.ContainsKey(ai))
                        {
                            stanceAssignments[ai] = ai.currentStance;
                        }
                    }
                }
            }
        }
    }

    private void AssignFormationSlots()
    {
        if (ActivePlayer == null) return;

        int slotIndex = 0;
        formationSlots.Clear();

        foreach (var ai in AllPartyAIs)
        {
            if (ai == null) continue;

            if (ai.gameObject != ActivePlayer)
            {
                if (slotIndex < followOffsets.Length)
                {
                    formationSlots[ai] = followOffsets[slotIndex];
                    slotIndex++;
                }
                else
                {
                    formationSlots[ai] = (slotIndex > 0) ? followOffsets[slotIndex - 1] : Vector3.zero;
                }
            }
        }
    }

    public Vector3 GetFormationPositionFor(PartyMemberAI ai)
    {
        GameObject leader = ActivePlayer;
        if (leader != null && formationSlots.TryGetValue(ai, out Vector3 offset))
        {
            return leader.transform.TransformPoint(offset);
        }
        if (leader != null && ai.gameObject != leader)
        {
            Vector3 defaultOffset = new Vector3(0, 0, -2f);
            formationSlots[ai] = defaultOffset;
            return leader.transform.TransformPoint(defaultOffset);
        }
        return ai.transform.position;
    }

    public void SetStanceForCharacter(GameObject character, AIStance newStance)
    {
        var ai = character.GetComponentInChildren<PartyMemberAI>(true);
        if (ai != null)
        {
            stanceAssignments[ai] = newStance;
            ai.currentStance = newStance;
            OnStanceChanged?.Invoke(character, newStance);
        }
    }

    // --- Command Methods ---
    public void IssueCommandAttack(GameObject target) { partyFocusTarget = target; foreach (var ai in AllPartyAIs) { if (ai != null && ai.enabled && ai.gameObject.activeInHierarchy && ai.gameObject != ActivePlayer) { ai.SetCommand(AICommand.AttackTarget, target); } } }
    public void IssueCommandMoveTo(Vector3 position) { partyFocusTarget = null; int offsetIndex = 0; foreach (var ai in AllPartyAIs) { if (ai != null && ai.enabled && ai.gameObject.activeInHierarchy && ai.gameObject != ActivePlayer) { Vector3 offset = Vector3.zero; if (offsetIndex < followOffsets.Length) { offset = followOffsets[offsetIndex]; offsetIndex++; } Vector3 individualPosition = position + offset; ai.SetCommand(AICommand.MoveToAndDefend, null, individualPosition); } } }
    public void IssueCommandFollow() { partyFocusTarget = null; foreach (var ai in AllPartyAIs) { if (ai != null && ai.enabled && ai.gameObject.activeInHierarchy) { ai.SetCommand(AICommand.Follow); } } }
    public void IssueCommandAttackSingle(GameObject nuc, GameObject target) { var ai = nuc.GetComponentInChildren<PartyMemberAI>(true); if (ai != null && ai.enabled) { ai.SetCommand(AICommand.AttackTarget, target); } }
    public void IssueCommandMoveToSingle(GameObject nuc, Vector3 position) { var ai = nuc.GetComponentInChildren<PartyMemberAI>(true); if (ai != null && ai.enabled) { ai.SetCommand(AICommand.MoveToAndDefend, null, position); } }
    public void CallForHelp(GameObject allyInNeed) { OnAllyNeedsHealing?.Invoke(allyInNeed); }
    public AIStance GetStanceForCharacter(GameObject character) { var ai = character.GetComponentInChildren<PartyMemberAI>(true); if (ai != null && stanceAssignments.TryGetValue(ai, out var stance)) { return stance; } return AIStance.Defensive; }
}