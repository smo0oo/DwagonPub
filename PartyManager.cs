using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.AI;

public class PartyManager : MonoBehaviour
{
    public static PartyManager instance;

    public static event Action<GameObject> OnActivePlayerChanged;

    [Header("Party Members")]
    public List<GameObject> partyMembers = new List<GameObject>();
    public GameObject ActivePlayer { get; private set; }
    private int activePlayerIndex = 0;

    public bool playerSwitchingEnabled { get; private set; } = true;

    [Header("Party Resources")]
    public int currencyGold = 0;

    [Header("Dome Battle Settings")]
    public int domeBasePower = 100;

    // --- NEW ---
    [Header("Movement Settings")]
    [Tooltip("The current movement mode for the active player.")]
    public PlayerMovement.MovementMode currentMovementMode = PlayerMovement.MovementMode.PointAndClick;
    // --- END NEW ---

    #region Leveling & Stats
    public event Action OnLevelUp;
    public event Action OnXPChanged;
    [Header("Party Leveling")]
    public int partyLevel = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 100;
    [Tooltip("How many stat points are awarded to EACH character per party level-up.")]
    public int pointsPerLevel = 5;
    [Tooltip("How many skill points are awarded to EACH character per party level-up.")]
    public int skillPointsPerLevel = 1;
    [Header("Stat Scaling Settings")]
    public float ratingToPercentDivisor = 50f;
    public float directDamageLevelRoot = 2f;
    [Header("Primary to Secondary Stat Conversion")]
    public float strengthConversionRate = 1.0f;
    public float agilityConversionRate = 1.0f;
    public float intelligenceConversionRate = 1.0f;
    public float faithConversionRate = 1.0f;
    public float armorResistanceConversionRate = 0.25f;
    #endregion

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    void Start()
    {
        if (partyMembers.Count > 0)
        {
            SetActivePlayer(0);
        }
    }

    void Update()
    {
        if (!playerSwitchingEnabled) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            int nextIndex = (activePlayerIndex + 1) % partyMembers.Count;

            while (nextIndex != activePlayerIndex)
            {
                if (partyMembers[nextIndex] != null && partyMembers[nextIndex].activeInHierarchy)
                {
                    SetActivePlayer(nextIndex);
                    return;
                }
                nextIndex = (nextIndex + 1) % partyMembers.Count;
            }
        }
    }

    // --- NEW METHOD ---
    /// <summary>
    /// Toggles the global movement mode and notifies the active player's movement script.
    /// </summary>
    public void ToggleMovementMode()
    {
        if (currentMovementMode == PlayerMovement.MovementMode.PointAndClick)
        {
            currentMovementMode = PlayerMovement.MovementMode.WASD;
        }
        else
        {
            currentMovementMode = PlayerMovement.MovementMode.PointAndClick;
        }

        // Notify the *currently active* player's movement script of the change
        if (ActivePlayer != null)
        {
            PlayerMovement pm = ActivePlayer.GetComponent<PlayerMovement>();
            if (pm != null && pm.enabled)
            {
                pm.OnMovementModeChanged(currentMovementMode);
            }
        }
    }
    // --- END NEW METHOD ---


    public void PrepareForCombatScene()
    {
        if (activePlayerIndex == 0 && playerSwitchingEnabled)
        {
            if (partyMembers.Count > 1)
            {
                SetActivePlayer(1);
            }
        }
    }

    public void SetActivePlayer(int index)
    {
        if (index < 0 || index >= partyMembers.Count) return;

        activePlayerIndex = index;
        ActivePlayer = partyMembers[activePlayerIndex];
        OnActivePlayerChanged?.Invoke(ActivePlayer);
    }

    public void SetPlayerSwitching(bool isEnabled)
    {
        playerSwitchingEnabled = isEnabled;
        if (!isEnabled && activePlayerIndex != 0)
        {
            SetActivePlayer(0);
        }
    }

    #region Leveling Methods
    public void AddExperience(int amount) { if (amount <= 0) return; currentXP += amount; while (currentXP >= xpToNextLevel) { LevelUp(); } OnXPChanged?.Invoke(); }
    public void SetLevel(int newLevel) { if (newLevel <= 0) return; int levelDifference = newLevel - partyLevel; partyLevel = newLevel; currentXP = 0; xpToNextLevel = CalculateXPForLevel(partyLevel + 1); if (levelDifference > 0) { DistributeStatPoints(levelDifference); } OnLevelUp?.Invoke(); OnXPChanged?.Invoke(); }
    private void LevelUp() { partyLevel++; currentXP -= xpToNextLevel; xpToNextLevel = CalculateXPForLevel(partyLevel + 1); DistributeStatPoints(1); OnLevelUp?.Invoke(); }
    private void DistributeStatPoints(int levelsGained) { if (partyMembers == null) return; foreach (GameObject playerGO in partyMembers) { PlayerStats stats = playerGO.GetComponentInChildren<PlayerStats>(); if (stats != null) { stats.unspentStatPoints += pointsPerLevel * levelsGained; stats.AddSkillPoints(skillPointsPerLevel * levelsGained); stats.CalculateFinalStats(); } } }
    private int CalculateXPForLevel(int level) { return 100 * level; }
    #endregion
}