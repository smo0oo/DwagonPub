using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Required for Async
using System;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    // Optional Event to notify UI when saving starts/ends
    public event Action OnSaveStarted;
    public event Action OnSaveCompleted;

    private PartyManager partyManager;
    private WorldMapManager worldMapManager;
    private WagonResourceManager wagonResourceManager;
    private DualModeManager dualModeManager;
    private List<GameObject> partyMembers;

    private bool isSaving = false;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); }
        else { instance = this; }
    }

    private string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "savegame.json");
    }

    private void FindManagerReferences()
    {
        partyManager = PartyManager.instance;
        worldMapManager = FindAnyObjectByType<WorldMapManager>();
        wagonResourceManager = FindAnyObjectByType<WagonResourceManager>();
        dualModeManager = DualModeManager.instance;

        if (partyManager != null)
        {
            partyMembers = partyManager.partyMembers;
        }
    }

    public void SaveGame()
    {
        if (isSaving) return; // Prevent double saves
        StartCoroutine(SaveGameAsyncRoutine());
    }

    // --- FIX: Explicitly use System.Collections.IEnumerator to avoid generic type errors ---
    private System.Collections.IEnumerator SaveGameAsyncRoutine()
    {
        isSaving = true;
        OnSaveStarted?.Invoke();

        // 1. Gather Data (MUST happen on Main Thread)
        FindManagerReferences();

        if (partyManager == null || partyMembers == null)
        {
            Debug.LogError("Save failed: Could not find required manager references.");
            isSaving = false;
            yield break;
        }

        SaveData data = new SaveData();

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
            data.dualModeData.dungeonTeamIndices = new List<int>(dualModeManager.dungeonTeamIndices);
            data.dualModeData.wagonTeamIndices = new List<int>(dualModeManager.wagonTeamIndices);

            data.dualModeData.dungeonLootBag = new List<ItemStackSaveData>();
            foreach (var stack in dualModeManager.dungeonLootBag)
            {
                if (stack.itemData != null)
                    data.dualModeData.dungeonLootBag.Add(new ItemStackSaveData(stack.itemData.id, stack.quantity));
            }

            if (dualModeManager.pendingBossBuff != null)
            {
                data.dualModeData.pendingBossBuffName = dualModeManager.pendingBossBuff.abilityName;
            }
        }

        // --- Collect Party Data ---
        data.partyLevel = partyManager.partyLevel;
        data.currentXP = partyManager.currentXP;
        data.xpToNextLevel = partyManager.xpToNextLevel;
        data.currencyGold = partyManager.currencyGold;

        // --- Collect Character Data ---
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
            foreach (var itemStack in inventory.items) { if (itemStack.itemData != null) { charData.inventoryItems.Add(new ItemStackSaveData(itemStack.itemData.id, itemStack.quantity)); } }
            foreach (var kvp in equipment.equippedItems) { if (kvp.Value != null && kvp.Value.itemData != null) { charData.equippedItems[kvp.Key] = new ItemStackSaveData(kvp.Value.itemData.id, kvp.Value.quantity); } }
            data.characterData.Add(charData);
        }

        // 2. Offload to Background Thread
        // Serialization and File writing are non-Unity thread safe
        Task saveTask = Task.Run(() =>
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetSavePath(), json);
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

    public void LoadGame()
    {
        var path = GetSavePath();
        if (!File.Exists(path))
        {
            Debug.LogWarning("No save file found.");
            return;
        }

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
        dmm.dungeonTeamIndices = new List<int>(saved.dungeonTeamIndices);
        dmm.wagonTeamIndices = new List<int>(saved.wagonTeamIndices);

        dmm.dungeonLootBag.Clear();
        if (InventoryManager.instance != null)
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
            dmm.pendingBossBuff = LoadBuffByName(saved.pendingBossBuffName);
        }

        Debug.Log("Dual Mode State Restored.");
    }

    private Ability LoadBuffByName(string abilityName)
    {
        return Resources.Load<Ability>("Abilities/" + abilityName);
    }
}