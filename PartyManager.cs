using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Collections;

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

    [Header("Movement Settings")]
    [Tooltip("The current movement mode for the active player.")]
    public PlayerMovement.MovementMode currentMovementMode = PlayerMovement.MovementMode.PointAndClick;

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
            int loopCount = 0;

            // Loop until we find a living member or check everyone
            while (loopCount < partyMembers.Count)
            {
                GameObject candidate = partyMembers[nextIndex];
                if (candidate != null && candidate.activeInHierarchy)
                {
                    Health h = candidate.GetComponentInChildren<Health>();
                    if (h != null && !h.isDowned && h.currentHealth > 0)
                    {
                        SetActivePlayer(nextIndex);
                        return;
                    }
                }
                nextIndex = (nextIndex + 1) % partyMembers.Count;
                loopCount++;
            }
        }
    }

    public void CheckPartyStatus()
    {
        if (GameManager.instance != null && (GameManager.instance.currentSceneType == SceneType.Town || GameManager.instance.currentSceneType == SceneType.MainMenu)) return;

        bool anySurvivor = false;
        bool activePlayerJustDied = false;

        if (ActivePlayer != null)
        {
            Health activeH = ActivePlayer.GetComponentInChildren<Health>();
            if (activeH != null && activeH.isDowned) activePlayerJustDied = true;
        }

        foreach (GameObject member in partyMembers)
        {
            if (member == null || !member.activeInHierarchy) continue;
            Health h = member.GetComponentInChildren<Health>();
            if (h != null && !h.isDowned && h.currentHealth > 0)
            {
                anySurvivor = true;
                break;
            }
        }

        if (!anySurvivor)
        {
            Debug.Log("PARTY WIPE DETECTED! Initiating failure sequence...");
            StartCoroutine(HandlePartyWipe());
        }
        else if (activePlayerJustDied && playerSwitchingEnabled)
        {
            SwitchToNextLivingMember();
        }
    }

    private void SwitchToNextLivingMember()
    {
        int startIndex = activePlayerIndex;
        int nextIndex = (activePlayerIndex + 1) % partyMembers.Count;

        while (nextIndex != startIndex)
        {
            GameObject candidate = partyMembers[nextIndex];
            if (candidate != null && candidate.activeInHierarchy)
            {
                Health h = candidate.GetComponentInChildren<Health>();
                if (h != null && !h.isDowned && h.currentHealth > 0)
                {
                    Debug.Log($"Active player down! Auto-switching to {candidate.name}");
                    SetActivePlayer(nextIndex);
                    return;
                }
            }
            nextIndex = (nextIndex + 1) % partyMembers.Count;
        }
    }

    private IEnumerator HandlePartyWipe()
    {
        yield return new WaitForSeconds(2.0f);
        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            Debug.Log("Dual Mode Wipe Detected. Initiating Rescue Logic...");
            DualModeManager.instance.StartRescueMission();
        }
        else
        {
            Debug.Log("Standard Party Wipe. Reloading last save...");
            if (SaveManager.instance != null) SaveManager.instance.LoadGame();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    public void ToggleMovementMode()
    {
        if (currentMovementMode == PlayerMovement.MovementMode.PointAndClick) currentMovementMode = PlayerMovement.MovementMode.WASD;
        else currentMovementMode = PlayerMovement.MovementMode.PointAndClick;
        if (ActivePlayer != null)
        {
            PlayerMovement pm = ActivePlayer.GetComponent<PlayerMovement>();
            if (pm != null && pm.enabled) pm.OnMovementModeChanged(currentMovementMode);
        }
    }

    public void PrepareForCombatScene()
    {
        if (activePlayerIndex == 0 && playerSwitchingEnabled)
        {
            if (partyMembers.Count > 1) SetActivePlayer(1);
        }
    }

    public void SetActivePlayer(int index)
    {
        if (index < 0 || index >= partyMembers.Count) return;

        GameObject candidate = partyMembers[index];
        if (candidate != null)
        {
            Health h = candidate.GetComponentInChildren<Health>();
            if (h != null && h.isDowned)
            {
                return;
            }
        }

        activePlayerIndex = index;
        ActivePlayer = partyMembers[activePlayerIndex];

        // --- COMPONENT TOGGLE LOGIC ---
        foreach (GameObject member in partyMembers)
        {
            if (member == null) continue;

            bool isMemberActive = (member == ActivePlayer);

            // 1. Toggle PlayerMovement
            PlayerMovement pm = member.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.enabled = isMemberActive;
            }

            // 2. Toggle PartyMemberAI
            PartyMemberAI ai = member.GetComponent<PartyMemberAI>();
            if (ai != null)
            {
                ai.enabled = !isMemberActive;
            }

            // 3. Toggle PartyMemberTargeting
            PartyMemberTargeting targeting = member.GetComponent<PartyMemberTargeting>();
            if (targeting != null)
            {
                targeting.enabled = !isMemberActive;
            }

            // 4. Toggle PartyMemberAbilitySelector
            PartyMemberAbilitySelector selector = member.GetComponent<PartyMemberAbilitySelector>();
            if (selector != null)
            {
                selector.enabled = !isMemberActive;
            }

            // 5. Safety: Reset path when becoming the active player to prevent ghost-walking
            if (isMemberActive)
            {
                NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh && agent.hasPath)
                {
                    agent.ResetPath();
                }
            }
        }
        // -----------------------------

        OnActivePlayerChanged?.Invoke(ActivePlayer);
    }

    public void SetPlayerSwitching(bool isEnabled)
    {
        playerSwitchingEnabled = isEnabled;
        if (!isEnabled && activePlayerIndex != 0) SetActivePlayer(0);
    }

    #region Leveling Methods
    public void AddExperience(int amount) { if (amount <= 0) return; currentXP += amount; while (currentXP >= xpToNextLevel) { LevelUp(); } OnXPChanged?.Invoke(); }
    public void SetLevel(int newLevel) { if (newLevel <= 0) return; int levelDifference = newLevel - partyLevel; partyLevel = newLevel; currentXP = 0; xpToNextLevel = CalculateXPForLevel(partyLevel + 1); if (levelDifference > 0) { DistributeStatPoints(levelDifference); } OnLevelUp?.Invoke(); OnXPChanged?.Invoke(); }
    private void LevelUp() { partyLevel++; currentXP -= xpToNextLevel; xpToNextLevel = CalculateXPForLevel(partyLevel + 1); DistributeStatPoints(1); OnLevelUp?.Invoke(); }
    private void DistributeStatPoints(int levelsGained) { if (partyMembers == null) return; foreach (GameObject playerGO in partyMembers) { PlayerStats stats = playerGO.GetComponentInChildren<PlayerStats>(); if (stats != null) { stats.unspentStatPoints += pointsPerLevel * levelsGained; stats.AddSkillPoints(skillPointsPerLevel * levelsGained); stats.CalculateFinalStats(); } } }
    private int CalculateXPForLevel(int level) { return 100 * level; }
    #endregion
}