using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    // --- SETTINGS ---
    [Header("Video Settings")]
    public int resolutionWidth = 1920;  // Default 1920
    public int resolutionHeight = 1080; // Default 1080
    public int refreshRate = 60;        // Default 60Hz
    public int fullscreenMode = 1;      // 0=Exclusive, 1=Borderless, 2=Windowed
    public int graphicsQualityIndex = 2; // Default High

    [Header("Audio Settings")]
    public float masterVolume = 1.0f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 1.0f;
    // ----------------

    // --- GAMEPLAY DATA ---
    [Header("World Data")]
    public float timeOfDay;
    public string currentLocationNodeID;

    // Stored as int to ensure serialization works easily across versions
    public int lastLocationType;

    [Header("Wagon Resources")]
    public float wagonFuel;
    public float wagonRations;
    public float wagonIntegrity;

    [Header("Dual Mode State")]
    public DualModeSaveData dualModeData = new DualModeSaveData();

    [Header("Party Data")]
    public int partyLevel;
    public int currentXP;
    public int xpToNextLevel;
    public int currencyGold;

    // Characters
    public List<CharacterSaveData> characterData = new List<CharacterSaveData>();
}

[Serializable]
public class DualModeSaveData
{
    public bool isDualModeActive;
    public bool isRescueMissionActive;
    public List<int> dungeonTeamIndices = new List<int>();
    public List<int> wagonTeamIndices = new List<int>();
    public List<ItemStackSaveData> dungeonLootBag = new List<ItemStackSaveData>();

    // We store the Ability Name to reload it later (requires Resources or a Lookup)
    public string pendingBossBuffName;
}

[Serializable]
public class CharacterSaveData
{
    public string characterPrefabID;
    public Vector3 position;
    public Quaternion rotation;
    public int unspentStatPoints;
    public int bonusStrength;
    public int bonusAgility;
    public int bonusIntelligence;
    public int bonusFaith;
    public List<string> unlockedAbilityBaseIDs = new List<string>();
    public List<int> unlockedAbilityRanks = new List<int>();
    public List<ItemStackSaveData> inventoryItems = new List<ItemStackSaveData>();
    public Dictionary<EquipmentType, ItemStackSaveData> equippedItems = new Dictionary<EquipmentType, ItemStackSaveData>();
    public int currentHealth;
}

[Serializable]
public class ItemStackSaveData
{
    public string itemID;
    public int quantity;

    public ItemStackSaveData(string id, int qty)
    {
        itemID = id;
        quantity = qty;
    }
}