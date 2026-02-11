using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WagonManager : MonoBehaviour
{
    public static WagonManager instance;

    [Header("Configuration")]
    [Tooltip("Parts automatically owned and equipped on a new game.")]
    public List<WagonUpgradeData> defaultUpgrades;

    // The parts currently physically on the wagon (Affects stats & visuals)
    // Key: Slot Type (e.g. Wheel) -> Value: The Data Asset
    private Dictionary<WagonUpgradeType, WagonUpgradeData> installedUpgrades = new Dictionary<WagonUpgradeType, WagonUpgradeData>();

    // The registry of ALL parts the player has ever unlocked/bought
    private HashSet<string> ownedUpgradeIDs = new HashSet<string>();

    // Stats (Publicly readable)
    public float TotalSpeedBonus { get; private set; }
    public float TotalEfficiencyBonus { get; private set; }
    public int TotalStorageBonus { get; private set; }
    public int TotalDefenseBonus { get; private set; }
    public float TotalComfortBonus { get; private set; }

    public event System.Action OnWagonUpgradesChanged;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // New Game Initialization
        if (installedUpgrades.Count == 0 && defaultUpgrades != null)
        {
            foreach (var upg in defaultUpgrades)
            {
                if (upg == null) continue;
                // Default items are free, owned, and immediately installed
                AddOwnedPart(upg.id);
                InstallUpgrade(upg, false);
            }
            RecalculateStats();
            // Notify listeners (like WagonVisualizer) just in case they are already listening
            OnWagonUpgradesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Installs a part onto the wagon. Overwrites any existing part in that slot.
    /// Also ensures the part is marked as Owned.
    /// </summary>
    public void InstallUpgrade(WagonUpgradeData data, bool notify = true)
    {
        if (data == null) return;

        // 1. Safeguard: Ensure we own it (in case code calls this directly)
        AddOwnedPart(data.id);

        // 2. Overwrite the Active Slot
        if (installedUpgrades.ContainsKey(data.type))
        {
            // Replaces the old part (e.g., Old Chest removed, New Chest added)
            installedUpgrades[data.type] = data;
        }
        else
        {
            installedUpgrades.Add(data.type, data);
        }

        // 3. Update Game State
        RecalculateStats();

        if (notify) OnWagonUpgradesChanged?.Invoke();
    }

    /// <summary>
    /// Grants ownership of a part without equipping it (e.g., Quest Reward, Loot).
    /// </summary>
    public void AddOwnedPart(string id)
    {
        if (!string.IsNullOrEmpty(id) && !ownedUpgradeIDs.Contains(id))
        {
            ownedUpgradeIDs.Add(id);
        }
    }

    /// <summary>
    /// Returns true if the player has ever bought/acquired this part.
    /// </summary>
    public bool IsPartOwned(string id)
    {
        return ownedUpgradeIDs.Contains(id);
    }

    public WagonUpgradeData GetInstalledUpgrade(WagonUpgradeType type)
    {
        if (installedUpgrades.TryGetValue(type, out var data)) return data;
        return null;
    }

    private void RecalculateStats()
    {
        TotalSpeedBonus = 0;
        TotalEfficiencyBonus = 0;
        TotalStorageBonus = 0;
        TotalDefenseBonus = 0;
        TotalComfortBonus = 0;

        foreach (var upg in installedUpgrades.Values)
        {
            if (upg == null) continue;
            TotalSpeedBonus += upg.speedBonus;
            TotalEfficiencyBonus += upg.efficiencyBonus;
            TotalStorageBonus += upg.storageSlotsAdded;
            TotalDefenseBonus += upg.defenseBonus;
            TotalComfortBonus += upg.comfortBonus;
        }

        // Hook: Update WagonController if it exists in the current scene
        WagonController controller = FindAnyObjectByType<WagonController>();
        // if (controller != null) controller.UpdateStats(TotalSpeedBonus); 
    }

    // --- SAVE/LOAD SYSTEM INTEGRATION ---

    [System.Serializable]
    public class WagonSaveData
    {
        public List<string> installedIDs = new List<string>();
        public List<string> ownedIDs = new List<string>();
    }

    public WagonSaveData GetSaveData()
    {
        return new WagonSaveData
        {
            installedIDs = installedUpgrades.Values.Select(u => u.id).ToList(),
            ownedIDs = ownedUpgradeIDs.ToList()
        };
    }

    public void LoadSaveData(WagonSaveData data, List<WagonUpgradeData> allDatabase)
    {
        if (data == null) return;

        // 1. Restore Ownership
        ownedUpgradeIDs.Clear();
        foreach (string id in data.ownedIDs) ownedUpgradeIDs.Add(id);

        // 2. Restore Installed Parts
        installedUpgrades.Clear();
        foreach (string id in data.installedIDs)
        {
            WagonUpgradeData part = allDatabase.FirstOrDefault(x => x.id == id);
            if (part != null) InstallUpgrade(part, false);
        }

        RecalculateStats();
        OnWagonUpgradesChanged?.Invoke();
    }
}