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

    // Always fetch the current active player dynamically
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
        // Start a loop to ensure AI list is populated once PartyManager is ready
        StartCoroutine(InitialPopulate());
    }

    void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
    }

    private IEnumerator InitialPopulate()
    {
        // Wait a frame to let other Start methods finish
        yield return null;
        RefreshAIList();
        AssignFormationSlots();
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

                // Ensure AI brain is OFF for the player we are controlling
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

                // Ensure AI brain is ON for followers
                SetAIComponentsActive(member, true);
            }
        }

        // Force an update after warping
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
                // In some games, the inactive party follows invisibly or is disabled. 
                // Adjust based on your specific combat design.
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

        // 1. Force a complete rebuild of the AI list
        RefreshAIList();

        // 2. Re-assign slots (this will now include the OLD player who is no longer active)
        AssignFormationSlots();

        // 3. Extra enforcement: Iterate all known party members and toggle AI components
        foreach (var member in partyManager.partyMembers)
        {
            if (member == null) continue;
            bool isLeader = (member == newActivePlayer);

            // If it's the leader, AI is OFF. If it's a follower, AI is ON.
            SetAIComponentsActive(member, !isLeader);
        }
    }

    // --- HELPER METHOD: Toggle AI Brain ---
    private void SetAIComponentsActive(GameObject root, bool isActive)
    {
        // Recursively find components even if they are currently disabled/inactive
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
                    // FIX: Use GetComponentInChildren(true) to find the script even if it's on a child object
                    // or if the object is currently disabled.
                    var ai = member.GetComponentInChildren<PartyMemberAI>(true);

                    if (ai != null)
                    {
                        AllPartyAIs.Add(ai);

                        // Ensure stance is tracked
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
        // If we don't have a leader, we can't make a formation
        if (ActivePlayer == null) return;

        int slotIndex = 0;
        formationSlots.Clear();

        foreach (var ai in AllPartyAIs)
        {
            if (ai == null) continue;

            // Important: We compare GameObjects to ensure we don't assign a follower slot 
            // to the character currently under player control.
            if (ai.gameObject != ActivePlayer)
            {
                if (slotIndex < followOffsets.Length)
                {
                    formationSlots[ai] = followOffsets[slotIndex];
                    slotIndex++;
                }
                else
                {
                    // Fallback if we run out of defined offsets
                    formationSlots[ai] = (slotIndex > 0) ? followOffsets[slotIndex - 1] : Vector3.zero;
                }
            }
        }
    }

    public Vector3 GetFormationPositionFor(PartyMemberAI ai)
    {
        GameObject leader = ActivePlayer;

        // 1. If we have a leader and this AI has a valid assigned slot
        if (leader != null && formationSlots.TryGetValue(ai, out Vector3 offset))
        {
            // Transform the local offset into world space relative to the leader
            return leader.transform.TransformPoint(offset);
        }

        // 2. Failsafe: If no slot is found (e.g., bug), try to assign one on the fly
        if (leader != null && ai.gameObject != leader)
        {
            // Auto-fix: Assign a default slot behind the player
            Vector3 defaultOffset = new Vector3(0, 0, -2f);
            formationSlots[ai] = defaultOffset;
            return leader.transform.TransformPoint(defaultOffset);
        }

        // 3. Absolute Fallback: Return current position
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