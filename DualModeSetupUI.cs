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

    private Dictionary<int, bool> assignments = new Dictionary<int, bool>();

    void Start()
    {
        if (startOperationButton != null)
            startOperationButton.onClick.AddListener(OnStartClicked);

        if (panelRoot != null) panelRoot.SetActive(false);

        // --- NEW: Auto-open using global NodeType enum ---
        if (GameManager.instance != null &&
            GameManager.instance.lastLocationType == NodeType.DualModeLocation)
        {
            Debug.Log("Dual Mode Location Detected: Auto-opening Setup UI.");
            OpenSetup();
        }
        // -------------------------------------------------
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
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void CloseSetup()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void RefreshUI()
    {
        foreach (Transform child in dungeonTeamContainer) Destroy(child.gameObject);
        foreach (Transform child in wagonTeamContainer) Destroy(child.gameObject);

        foreach (var kvp in assignments)
        {
            int memberIndex = kvp.Key;
            bool isDungeon = kvp.Value;

            GameObject memberObj = PartyManager.instance.partyMembers[memberIndex];
            if (memberObj == null) continue;

            Transform parent = isDungeon ? dungeonTeamContainer : wagonTeamContainer;
            GameObject btnObj = Instantiate(memberButtonPrefab, parent);

            TextMeshProUGUI text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = memberObj.name;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => ToggleTeam(memberIndex));
        }

        ValidateStart();
    }

    private void ToggleTeam(int index)
    {
        if (assignments.ContainsKey(index))
        {
            assignments[index] = !assignments[index];
            RefreshUI();
        }
    }

    private void ValidateStart()
    {
        bool hasDungeonMember = false;
        foreach (var kvp in assignments)
        {
            if (kvp.Value == true)
            {
                hasDungeonMember = true;
                break;
            }
        }

        if (startOperationButton != null)
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

        if (DualModeManager.instance != null)
        {
            DualModeManager.instance.InitializeSplit(groupA, groupB, domeDefenseSceneName);
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.LoadLevel(firstDungeonSceneName, dungeonSpawnPointID);
        }

        CloseSetup();
    }
}