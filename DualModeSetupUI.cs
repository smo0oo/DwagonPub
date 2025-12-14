using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DualModeSetupUI : MonoBehaviour
{
    [Header("UI Containers")]
    public Transform dungeonTeamContainer;
    public Transform wagonTeamContainer;
    public GameObject memberButtonPrefab;
    public Button startOperationButton;
    public GameObject panelRoot;

    [Header("Settings")]
    [Tooltip("The name of the scene to load for the Dungeon part.")]
    public string firstDungeonSceneName = "Dungeon_Level1";
    [Tooltip("The SpawnPoint ID to use in the dungeon.")]
    public string dungeonSpawnPointID = "Entrance";

    [Tooltip("The name of the Dome Battle scene to load when switching to Group B (e.g. 'DomeBattle_Forest').")]
    public string domeDefenseSceneName = "DomeBattle";

    // Track where each member is currently assigned
    // Key: Party Member Index, Value: Is in Dungeon Team? (True=Dungeon, False=Wagon)
    private Dictionary<int, bool> assignments = new Dictionary<int, bool>();

    void Start()
    {
        if (startOperationButton != null)
            startOperationButton.onClick.AddListener(OnStartClicked);

        // Start hidden
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void OpenSetup()
    {
        if (PartyManager.instance == null) return;

        assignments.Clear();

        // Default: Everyone (except Player 0) is on the Wagon Team
        for (int i = 1; i < PartyManager.instance.partyMembers.Count; i++)
        {
            assignments[i] = false; // False = Wagon Team
        }

        RefreshUI();
        panelRoot.SetActive(true);
    }

    public void CloseSetup()
    {
        panelRoot.SetActive(false);
    }

    private void RefreshUI()
    {
        // Clear containers
        foreach (Transform child in dungeonTeamContainer) Destroy(child.gameObject);
        foreach (Transform child in wagonTeamContainer) Destroy(child.gameObject);

        // Rebuild lists
        foreach (var kvp in assignments)
        {
            int memberIndex = kvp.Key;
            bool isDungeon = kvp.Value;

            GameObject memberObj = PartyManager.instance.partyMembers[memberIndex];
            if (memberObj == null) continue;

            // Create button
            Transform parent = isDungeon ? dungeonTeamContainer : wagonTeamContainer;
            GameObject btnObj = Instantiate(memberButtonPrefab, parent);

            // Setup visual text
            TextMeshProUGUI text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = memberObj.name;

            // Setup click event to swap teams
            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => ToggleTeam(memberIndex));
        }

        ValidateStart();
    }

    private void ToggleTeam(int index)
    {
        if (assignments.ContainsKey(index))
        {
            assignments[index] = !assignments[index]; // Flip boolean
            RefreshUI();
        }
    }

    private void ValidateStart()
    {
        // Require at least 1 person in Dungeon Team
        bool hasDungeonMember = false;
        foreach (var kvp in assignments)
        {
            if (kvp.Value == true)
            {
                hasDungeonMember = true;
                break;
            }
        }

        startOperationButton.interactable = hasDungeonMember;
    }

    private void OnStartClicked()
    {
        List<int> groupA = new List<int>();
        List<int> groupB = new List<int>();

        foreach (var kvp in assignments)
        {
            if (kvp.Value) groupA.Add(kvp.Key);
            else groupB.Add(kvp.Key);
        }

        // Initialize the Manager with the specific dome scene
        if (DualModeManager.instance != null)
        {
            DualModeManager.instance.InitializeSplit(groupA, groupB, domeDefenseSceneName);
        }

        // Trigger the transition logic
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadLevel(firstDungeonSceneName, dungeonSpawnPointID);
        }

        CloseSetup();
    }
}