using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    private PartyManager partyManager;
    private WorldMapManager worldMapManager;
    private WagonResourceManager wagonResourceManager;
    private DualModeManager dualModeManager; // Reference to Dual Mode
    private List<GameObject> partyMembers;

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
        dualModeManager = DualModeManager.instance; // Find the singleton

        if (partyManager != null)
        {
            partyMembers = partyManager.partyMembers;
        }
    }

    public void SaveGame()
    {
        FindManagerReferences();

        if (partyManager == null || partyMembers == null)
        {
            Debug.LogError("Save failed: Could not find required manager references.");
            return;
        }

        SaveData data = new SaveData();

        // --- Save Wagon Data ---
        if (wagonResourceManager != null)
        {
            data.wagonFuel = wagonResourceManager.currentFuel;
            data.wagonRations = wagonResourceManager.currentRations;
            data.wagonIntegrity = wagonResourceManager.currentIntegrity;
        }

        // --- Save World Data ---
        if (worldMapManager != null)
        {
            data.timeOfDay = worldMapManager.timeOfDay;
            data.currentLocationNodeID = worldMapManager.currentLocation.locationName;
        }
        else if (GameManager.instance != null)
        {
            data.currentLocationNodeID = GameManager.instance.lastKnownLocationNodeID;
        }

        // --- Save Dual Mode Data ---
        if (dualModeManager != null)
        {
            data.dualModeData.isDualModeActive = dualModeManager.isDualModeActive;
            data.dualModeData.isRescueMissionActive = dualModeManager.isRescueMissionActive;
            data.dualModeData.dungeonTeamIndices = new List<int>(dualModeManager.dungeonTeamIndices);
            data.dualModeData.wagonTeamIndices = new List<int>(dualModeManager.wagonTeamIndices);

            // Save Loot Bag
            data.dualModeData.dungeonLootBag = new List<ItemStackSaveData>();
            foreach (var stack in dualModeManager.dungeonLootBag)
            {
                if (stack.itemData != null)
                    data.dualModeData.dungeonLootBag.Add(new ItemStackSaveData(stack.itemData.id, stack.quantity));
            }

            // Save Boss Buff Name
            if (dualModeManager.pendingBossBuff != null)
            {
                data.dualModeData.pendingBossBuffName = dualModeManager.pendingBossBuff.abilityName;
            }
        }
        // ---------------------------

        // --- Save Party Data ---
        data.partyLevel = partyManager.partyLevel;
        data.currentXP = partyManager.currentXP;
        data.xpToNextLevel = partyManager.xpToNextLevel;
        data.currencyGold = partyManager.currencyGold;

        // --- Save Character Data ---
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

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(), json);
        Debug.Log("Game Saved to: " + GetSavePath());
    }

    public void LoadGame()
    {
        var path = GetSavePath();
        if (!File.Exists(path))
        {
            Debug.LogWarning("No save file found.");
            return;
        }

        // Just trigger the GameManager sequence; it handles reading the JSON now
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadSavedGame();
        }
    }

    // --- NEW HELPER: Called by GameManager or DualModeManager after loading the file ---
    public void RestoreDualModeState(SaveData data)
    {
        if (DualModeManager.instance == null || data.dualModeData == null) return;

        var dmm = DualModeManager.instance;
        var saved = data.dualModeData;

        dmm.isDualModeActive = saved.isDualModeActive;
        dmm.isRescueMissionActive = saved.isRescueMissionActive;
        dmm.dungeonTeamIndices = new List<int>(saved.dungeonTeamIndices);
        dmm.wagonTeamIndices = new List<int>(saved.wagonTeamIndices);

        // Restore Loot Bag
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

        // Restore Boss Buff
        if (!string.IsNullOrEmpty(saved.pendingBossBuffName))
        {
            dmm.pendingBossBuff = LoadBuffByName(saved.pendingBossBuffName);
        }

        Debug.Log("Dual Mode State Restored.");
    }

    private Ability LoadBuffByName(string abilityName)
    {
        // ASSUMPTION: Abilities are in a Resources folder. 
        // Adjust path if your abilities are stored differently (e.g. ScriptableObject database).
        return Resources.Load<Ability>("Abilities/" + abilityName);
    }
}