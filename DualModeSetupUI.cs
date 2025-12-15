using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class DualModeSetupUI : MonoBehaviour
{
    [Header("UI Containers")]
    public Transform dungeonTeamContainer;
    public Transform wagonTeamContainer;
    public GameObject memberButtonPrefab;
    public Button startOperationButton;
    public GameObject panelRoot;

    [Header("Settings")]
    public string firstDungeonSceneName = "Dungeon_Level1";
    public string dungeonSpawnPointID = "Entrance";
    public string domeDefenseSceneName = "DomeBattle";

    private Dictionary<int, bool> assignments = new Dictionary<int, bool>();

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (startOperationButton != null)
        {
            startOperationButton.onClick.RemoveListener(OnStartClicked);
            startOperationButton.onClick.AddListener(OnStartClicked);
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndOpenSetup();
    }

    private void CheckAndOpenSetup()
    {
        if (GameManager.instance == null) return;

        // 1. Is the mission already running? If so, DO NOT open setup.
        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            return;
        }

        // 2. Did we just finish a run? If so, DO NOT open setup.
        // --- NEW CHECK ---
        if (GameManager.instance.justExitedDungeon)
        {
            Debug.Log("DualModeSetupUI: Player just exited dungeon. Keeping Setup UI closed.");
            return;
        }
        // -----------------

        // 3. Are we in a Dual Mode Location?
        if (GameManager.instance.lastLocationType == NodeType.DualModeLocation)
        {
            Debug.Log($"DualModeSetupUI: Opening Setup UI for location type '{GameManager.instance.lastLocationType}' in scene '{SceneManager.GetActiveScene().name}'.");
            OpenSetup();
        }
    }

    public void OpenSetup()
    {
        if (PartyManager.instance == null) return;

        assignments.Clear();
        for (int i = 1; i < PartyManager.instance.partyMembers.Count; i++)
        {
            assignments[i] = false;
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