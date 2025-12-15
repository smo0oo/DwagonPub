using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using PixelCrushers.DialogueSystem;

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

    [Header("Team Constraints")]
    [Tooltip("Minimum members required in EACH team (excluding Player 0).")]
    public int minTeamMembers = 2;
    [Tooltip("Maximum members allowed in EACH team (excluding Player 0).")]
    public int maxTeamMembers = 2;

    private Dictionary<int, bool> assignments = new Dictionary<int, bool>();

    // Property to check visibility
    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

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

        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            return;
        }

        if (GameManager.instance.justExitedDungeon)
        {
            Debug.Log("DualModeSetupUI: Player just exited dungeon. Keeping Setup UI closed.");
            return;
        }

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
        // Default: Everyone (except Player 0) is on the Wagon Team
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

            // --- 1. SET NAME ---
            TextMeshProUGUI text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            string displayName = memberObj.name;

            DialogueActor da = memberObj.GetComponentInChildren<DialogueActor>();
            if (da != null && !string.IsNullOrEmpty(da.actor))
            {
                var actor = DialogueManager.MasterDatabase.GetActor(da.actor);
                if (actor != null)
                {
                    displayName = actor.LookupValue("Display Name") ?? actor.Name;
                }
            }
            if (text != null) text.text = displayName;

            // --- 2. SET PORTRAIT ---
            Transform portraitTrans = btnObj.transform.Find("Portrait");
            if (portraitTrans != null)
            {
                Image portraitImg = portraitTrans.GetComponent<Image>();
                if (portraitImg != null)
                {
                    if (da != null)
                    {
                        var actor = DialogueManager.MasterDatabase.GetActor(da.actor);
                        if (actor != null && actor.portrait != null)
                        {
                            Texture2D tex = actor.portrait;
                            portraitImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                            portraitImg.enabled = true;
                        }
                        else
                        {
                            portraitImg.enabled = false;
                        }
                    }
                    else
                    {
                        portraitImg.enabled = false;
                    }
                }
            }

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => ToggleTeam(memberIndex));
        }

        // --- VALIDATE BUTTON STATE ---
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

    // --- UPDATED VALIDATION LOGIC ---
    private void ValidateStart()
    {
        int dungeonCount = 0;
        int wagonCount = 0;

        foreach (var kvp in assignments)
        {
            if (kvp.Value == true) dungeonCount++; // True = Dungeon
            else wagonCount++; // False = Wagon
        }

        // Check constraints for BOTH teams
        bool dungeonValid = (dungeonCount >= minTeamMembers && dungeonCount <= maxTeamMembers);
        bool wagonValid = (wagonCount >= minTeamMembers && wagonCount <= maxTeamMembers);

        if (startOperationButton != null)
        {
            // Only enable button if BOTH teams meet the criteria
            startOperationButton.interactable = (dungeonValid && wagonValid);
        }
    }
    // -------------------------------

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