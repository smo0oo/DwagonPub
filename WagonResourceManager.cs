using UnityEngine;
using UnityEngine.Events;

public class WagonResourceManager : MonoBehaviour
{
    public static WagonResourceManager instance;

    [Header("Resources")]
    public float currentRations = 100f;
    public float maxRations = 200f;
    public float currentFuel = 100f;
    public float maxFuel = 200f;
    public float currentIntegrity = 100f;
    public float maxIntegrity = 100f;

    [Header("Consumption Rates (Per Hour)")]
    public float rationsPerHour = 2f;
    public float fuelPerHour = 5f;

    [Header("Events")]
    public UnityEvent OnResourcesChanged;
    public UnityEvent OnStarvationStarted;
    public UnityEvent OnFuelDepleted;
    public UnityEvent OnWagonBroken;

    private bool isStarving = false;
    private bool isOutOfFuel = false;

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

    // --- EXISTING: Used when moving ---
    public void ConsumeForTravel(float hoursTraveled)
    {
        // Consume Rations
        ConsumeRations(hoursTraveled);

        // Consume Fuel (Only when moving)
        if (currentFuel > 0)
        {
            currentFuel -= fuelPerHour * hoursTraveled;
            if (currentFuel <= 0)
            {
                currentFuel = 0;
                if (!isOutOfFuel)
                {
                    isOutOfFuel = true;
                    OnFuelDepleted?.Invoke();
                    if (WorldMapManager.instance != null) WorldMapManager.instance.ApplyNoFuelPenalty();
                }
            }
        }
        OnResourcesChanged?.Invoke();
    }

    // --- NEW: Used when Foraging / Waiting ---
    public void ConsumeForTime(float hoursPassed)
    {
        // Only consume Rations, NOT Fuel
        ConsumeRations(hoursPassed);
        OnResourcesChanged?.Invoke();
    }

    // Helper to avoid duplicate code
    private void ConsumeRations(float duration)
    {
        if (currentRations > 0)
        {
            currentRations -= rationsPerHour * duration;
            if (currentRations <= 0)
            {
                currentRations = 0;
                if (!isStarving)
                {
                    isStarving = true;
                    OnStarvationStarted?.Invoke();
                    Debug.Log("The party is starving!");
                }
            }
        }
    }

    public void AddResource(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.Rations:
                currentRations = Mathf.Clamp(currentRations + amount, 0, maxRations);
                if (currentRations > 0) isStarving = false;
                break;
            case ResourceType.Fuel:
                currentFuel = Mathf.Clamp(currentFuel + amount, 0, maxFuel);
                if (currentFuel > 0 && isOutOfFuel)
                {
                    isOutOfFuel = false;
                    if (WorldMapManager.instance != null) WorldMapManager.instance.RemoveNoFuelPenalty();
                }
                break;
            case ResourceType.Integrity:
                currentIntegrity = Mathf.Clamp(currentIntegrity + amount, 0, maxIntegrity);
                break;
            case ResourceType.Gold:
                if (PartyManager.instance != null) PartyManager.instance.currencyGold += amount;
                break;
        }
        OnResourcesChanged?.Invoke();
    }
}