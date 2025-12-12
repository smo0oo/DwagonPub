using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    // World
    public float timeOfDay;
    public string currentLocationNodeID;

    // --- NEW: Wagon Resources ---
    public float wagonFuel;
    public float wagonRations;
    public float wagonIntegrity;
    // ----------------------------

    // Party
    public int partyLevel;
    public int currentXP;
    public int xpToNextLevel;
    public int currencyGold; // Ensure Gold is saved here too

    // Characters
    public List<CharacterSaveData> characterData = new();
}

// ... (CharacterSaveData and ItemStackSaveData remain the same) ...
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
    public List<string> unlockedAbilityBaseIDs = new();
    public List<int> unlockedAbilityRanks = new();
    public List<ItemStackSaveData> inventoryItems = new();
    public Dictionary<EquipmentType, ItemStackSaveData> equippedItems = new();
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