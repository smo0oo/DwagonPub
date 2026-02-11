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
                AddOwnedPart(upg.id);
                InstallUpgrade(upg, false);
            }
            RecalculateStats();
            OnWagonUpgradesChanged?.Invoke();
        }
    }

    // --- NEW: Tag Logic ---
    /// <summary>
    /// Checks if the installed upgrades provide ALL the required tags.
    /// </summary>
    public bool HasTraversalTags(List<string> requiredTags)
    {
        if (requiredTags == null || requiredTags.Count == 0) return true;

        // 1. Gather all tags from currently INSTALLED parts
        HashSet<string> myTags = new HashSet<string>();
        foreach (var part in installedUpgrades.Values)
        {
            if (part != null && part.traversalTags != null)
            {
                foreach (string tag in part.traversalTags) myTags.Add(tag);
            }
        }

        // 2. Check if we meet every requirement
        foreach (string req in requiredTags)
        {
            if (!myTags.Contains(req)) return false; // Missing a required tag
        }

        return true;
    }
    // -------------------------

    public void InstallUpgrade(WagonUpgradeData data, bool notify = true)
    {
        if (data == null) return;

        AddOwnedPart(data.id);

        if (installedUpgrades.ContainsKey(data.type))
        {
            installedUpgrades[data.type] = data;
        }
        else
        {
            installedUpgrades.Add(data.type, data);
        }

        RecalculateStats();
        if (notify) OnWagonUpgradesChanged?.Invoke();
    }

    public void AddOwnedPart(string id)
    {
        if (!string.IsNullOrEmpty(id) && !ownedUpgradeIDs.Contains(id))
        {
            ownedUpgradeIDs.Add(id);
        }
    }

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
        TotalSpeedBonus = 0; TotalEfficiencyBonus = 0; TotalStorageBonus = 0; TotalDefenseBonus = 0; TotalComfortBonus = 0;

        foreach (var upg in installedUpgrades.Values)
        {
            if (upg == null) continue;
            TotalSpeedBonus += upg.speedBonus;
            TotalEfficiencyBonus += upg.efficiencyBonus;
            TotalStorageBonus += upg.storageSlotsAdded;
            TotalDefenseBonus += upg.defenseBonus;
            TotalComfortBonus += upg.comfortBonus;
        }

        WagonController controller = FindAnyObjectByType<WagonController>();
        // if (controller != null) controller.UpdateStats(TotalSpeedBonus); // Uncomment if implementing stats
    }

    // --- SAVE/LOAD ---
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

        ownedUpgradeIDs.Clear();
        foreach (string id in data.ownedIDs) ownedUpgradeIDs.Add(id);

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