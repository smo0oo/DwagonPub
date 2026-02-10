using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WagonManager : MonoBehaviour
{
    public static WagonManager instance;

    [Header("Configuration")]
    public List<WagonUpgradeData> defaultUpgrades; // Starting parts

    // The persistent state: Slot Type -> Upgrade Data
    private Dictionary<WagonUpgradeType, WagonUpgradeData> installedUpgrades = new Dictionary<WagonUpgradeType, WagonUpgradeData>();

    // Cached Stats (Calculated on change)
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
        // If we haven't loaded a save, equip defaults
        if (installedUpgrades.Count == 0 && defaultUpgrades != null)
        {
            foreach (var upg in defaultUpgrades)
            {
                InstallUpgrade(upg, false); // Don't trigger event yet
            }
            RecalculateStats();
        }
    }

    public void InstallUpgrade(WagonUpgradeData data, bool notify = true)
    {
        if (data == null) return;

        if (installedUpgrades.ContainsKey(data.type))
        {
            installedUpgrades[data.type] = data; // Swap out old part
        }
        else
        {
            installedUpgrades.Add(data.type, data);
        }

        RecalculateStats();
        if (notify) OnWagonUpgradesChanged?.Invoke();
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
            TotalSpeedBonus += upg.speedBonus;
            TotalEfficiencyBonus += upg.efficiencyBonus;
            TotalStorageBonus += upg.storageSlotsAdded;
            TotalDefenseBonus += upg.defenseBonus;
            TotalComfortBonus += upg.comfortBonus;
        }

        // Optional: Push updates to WagonController if it exists in the scene
        WagonController controller = FindAnyObjectByType<WagonController>();
        if (controller != null)
        {
            // You would add a method like controller.UpdateSpeed(TotalSpeedBonus);
        }
    }

    // --- SAVE/LOAD INTEGRATION ---
    // Call these from your SaveManager
    public List<string> GetSaveData()
    {
        return installedUpgrades.Values.Select(u => u.id).ToList();
    }

    public void LoadSaveData(List<string> upgradeIDs, List<WagonUpgradeData> allDatabase)
    {
        installedUpgrades.Clear();
        foreach (string id in upgradeIDs)
        {
            WagonUpgradeData data = allDatabase.FirstOrDefault(x => x.id == id);
            if (data != null) InstallUpgrade(data, false);
        }
        RecalculateStats();
        OnWagonUpgradesChanged?.Invoke();
    }
}