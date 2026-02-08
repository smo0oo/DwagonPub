using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    // Optional Event to notify UI when saving starts/ends
    public event Action OnSaveStarted;
    public event Action OnSaveCompleted;

    public SaveData CurrentSaveData = new SaveData();

    private PartyManager partyManager;
    private WorldMapManager worldMapManager;
    private WagonResourceManager wagonResourceManager;
    private DualModeManager dualModeManager;
    private List<GameObject> partyMembers;

    private bool isSaving = false;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadDataOnly(); // Load settings on startup
        }
    }

    private string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "savegame.json");
    }

    private void FindManagerReferences()
    {
        partyManager = PartyManager.instance;
        // Use FindFirstObjectByType for Unity 2023+, or FindObjectOfType for older
        worldMapManager = FindFirstObjectByType<WorldMapManager>();
        wagonResourceManager = FindFirstObjectByType<WagonResourceManager>();
        dualModeManager = DualModeManager.instance;

        if (partyManager != null)
        {
            partyMembers = partyManager.partyMembers;
        }
    }

    public void SaveGame()
    {
        if (isSaving) return;
        StartCoroutine(SaveGameAsyncRoutine());
    }

    private System.Collections.IEnumerator SaveGameAsyncRoutine()
    {
        isSaving = true;
        OnSaveStarted?.Invoke();

        // 1. Gather Data (Main Thread)
        FindManagerReferences();

        // Use existing data container or create new if null
        SaveData data = CurrentSaveData;
        if (data == null) data = new SaveData();

        // Check if we are in Gameplay or Main Menu
        bool isGameplayActive = true;
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu)
        {
            isGameplayActive = false;
        }

        if (isGameplayActive)
        {
            // --- Collect Wagon Data ---
            if (wagonResourceManager != null)
            {
                data.wagonFuel = wagonResourceManager.currentFuel;
                data.wagonRations = wagonResourceManager.currentRations;
                data.wagonIntegrity = wagonResourceManager.currentIntegrity;
            }

            // --- Collect World Data ---
            if (worldMapManager != null)
            {
                data.timeOfDay = worldMapManager.timeOfDay;
                if (worldMapManager.currentLocation != null)
                    data.currentLocationNodeID = worldMapManager.currentLocation.locationName;
            }
            else if (GameManager.instance != null)
            {
                data.currentLocationNodeID = GameManager.instance.lastKnownLocationNodeID;
            }

            if (GameManager.instance != null)
            {
                data.lastLocationType = (int)GameManager.instance.lastLocationType;
            }

            // --- Collect Dual Mode Data ---
            if (dualModeManager != null)
            {
                data.dualModeData.isDualModeActive = dualModeManager.isDualModeActive;
                data.dualModeData.isRescueMissionActive = dualModeManager.isRescueMissionActive;

                if (dualModeManager.dungeonTeamIndices != null)
                    data.dualModeData.dungeonTeamIndices = new List<int>(dualModeManager.dungeonTeamIndices);

                if (dualModeManager.wagonTeamIndices != null)
                    data.dualModeData.wagonTeamIndices = new List<int>(dualModeManager.wagonTeamIndices);

                data.dualModeData.dungeonLootBag = new List<ItemStackSaveData>();
                if (dualModeManager.dungeonLootBag != null)
                {
                    foreach (var stack in dualModeManager.dungeonLootBag)
                    {
                        if (stack != null && stack.itemData != null)
                            data.dualModeData.dungeonLootBag.Add(new ItemStackSaveData(stack.itemData.id, stack.quantity));
                    }
                }

                if (dualModeManager.pendingBossBuff != null)
                {
                    data.dualModeData.pendingBossBuffName = dualModeManager.pendingBossBuff.abilityName;
                }
            }

            // --- Collect Party Data ---
            if (partyManager != null)
            {
                data.partyLevel = partyManager.partyLevel;
                data.currentXP = partyManager.currentXP;
                data.xpToNextLevel = partyManager.xpToNextLevel;
                data.currencyGold = partyManager.currencyGold;

                if (partyMembers != null)
                {
                    data.characterData = new List<CharacterSaveData>();
                    foreach (var member in partyMembers)
                    {
                        if (member == null) continue;
                        var stats = member.GetComponentInChildren<PlayerStats>(true);
                        var inventory = member.GetComponentInChildren<Inventory>(true);
                        var equipment = member.GetComponentInChildren<PlayerEquipment>(true);
                        var health = member.GetComponentInChildren<Health>(true);
                        if (stats == null || inventory == null || equipment == null || health == null) continue;

                        var charData = new CharacterSaveData
                        {
                            characterPrefabID = member.name,
                            position = member.transform.position,
                            rotation = member.transform.rotation,
                            unspentStatPoints = stats.unspentStatPoints,
                            bonusStrength = stats.bonusStrength,
                            bonusAgility = stats.bonusAgility,
                            bonusIntelligence = stats.bonusIntelligence,
                            bonusFaith = stats.bonusFaith,
                            unlockedAbilityBaseIDs = new List<string>(),
                            unlockedAbilityRanks = new List<int>(),
                            inventoryItems = new List<ItemStackSaveData>(),
                            equippedItems = new Dictionary<EquipmentType, ItemStackSaveData>(),
                            currentHealth = health.currentHealth
                        };

                        foreach (var kvp in stats.unlockedAbilityRanks) { charData.unlockedAbilityBaseIDs.Add(kvp.Key.abilityName); charData.unlockedAbilityRanks.Add(kvp.Value); }
                        foreach (var itemStack in inventory.items) { if (itemStack != null && itemStack.itemData != null) { charData.inventoryItems.Add(new ItemStackSaveData(itemStack.itemData.id, itemStack.quantity)); } }
                        foreach (var kvp in equipment.equippedItems) { if (kvp.Value != null && kvp.Value.itemData != null) { charData.equippedItems[kvp.Key] = new ItemStackSaveData(kvp.Value.itemData.id, kvp.Value.quantity); } }
                        data.characterData.Add(charData);
                    }
                }
            }
        }

        CurrentSaveData = data;

        // --- FIX: Prepare data on Main Thread before Threading ---
        string json = JsonUtility.ToJson(data, true);
        string path = GetSavePath();
        // ---------------------------------------------------------

        // 2. Offload ONLY File Writing to Background Thread
        Task saveTask = Task.Run(() =>
        {
            // Now safe because path and json are simple strings passed in
            File.WriteAllText(path, json);
        });

        // 3. Wait for completion
        while (!saveTask.IsCompleted)
        {
            yield return null;
        }

        if (saveTask.IsFaulted)
        {
            Debug.LogError($"Save Failed: {saveTask.Exception}");
        }
        else
        {
            Debug.Log("Game Saved Successfully (Async).");
        }

        isSaving = false;
        OnSaveCompleted?.Invoke();
    }

    private void LoadDataOnly()
    {
        var path = GetSavePath();
        if (!File.Exists(path))
        {
            CurrentSaveData = new SaveData();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            CurrentSaveData = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to parse save file: " + e.Message);
            CurrentSaveData = new SaveData();
        }
    }

    public void LoadGame()
    {
        LoadDataOnly();

        if (GameManager.instance != null)
        {
            GameManager.instance.LoadSavedGame();
        }
    }

    public void RestoreDualModeState(SaveData data)
    {
        if (DualModeManager.instance == null || data.dualModeData == null) return;

        var dmm = DualModeManager.instance;
        var saved = data.dualModeData;

        dmm.isDualModeActive = saved.isDualModeActive;
        dmm.isRescueMissionActive = saved.isRescueMissionActive;

        if (saved.dungeonTeamIndices != null)
            dmm.dungeonTeamIndices = new List<int>(saved.dungeonTeamIndices);

        if (saved.wagonTeamIndices != null)
            dmm.wagonTeamIndices = new List<int>(saved.wagonTeamIndices);

        dmm.dungeonLootBag.Clear();
        if (InventoryManager.instance != null && saved.dungeonLootBag != null)
        {
            foreach (var savedStack in saved.dungeonLootBag)
            {
                ItemData item = InventoryManager.instance.GetItemByID(savedStack.itemID);
                if (item != null)
                {
                    dmm.dungeonLootBag.Add(new ItemStack(item, savedStack.quantity));
                }
            }
        }

        if (!string.IsNullOrEmpty(saved.pendingBossBuffName))
        {
            dmm.pendingBossBuff = Resources.Load<Ability>("Abilities/" + saved.pendingBossBuffName);
        }

        Debug.Log("Dual Mode State Restored.");
    }
}