using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NotificationUIManager : MonoBehaviour
{
    [Header("Notification Buttons")]
    [Tooltip("The button/icon that appears when you have unspent STAT points.")]
    public GameObject attributePointsButton;

    [Tooltip("The button/icon that appears when you have unspent SKILL points.")]
    public GameObject skillPointsButton;

    [Header("Optional Text Counters")]
    [Tooltip("If set, this text will show the number of unspent Attribute points.")]
    public TextMeshProUGUI attributeCountText;

    [Tooltip("If set, this text will show the number of unspent Skill points.")]
    public TextMeshProUGUI skillCountText;

    private PlayerStats _currentStats;

    void Start()
    {
        // 1. Subscribe to Global Party Events
        if (PartyManager.instance != null)
        {
            PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;

            // Initialize with the currently active player
            HandleActivePlayerChanged(PartyManager.instance.ActivePlayer);
        }

        // 2. Subscribe to Static Stats Event (Fires for any stats change)
        PlayerStats.OnStatsChanged += RefreshUI;

        // Initial state check
        RefreshUI();
    }

    void OnDestroy()
    {
        if (PartyManager.instance != null)
        {
            PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
        }

        PlayerStats.OnStatsChanged -= RefreshUI;

        // Clean up instance subscription
        if (_currentStats != null)
        {
            _currentStats.OnSkillPointsChanged -= RefreshUI;
        }
    }

    private void HandleActivePlayerChanged(GameObject newPlayer)
    {
        // 1. Unsubscribe from old player
        if (_currentStats != null)
        {
            _currentStats.OnSkillPointsChanged -= RefreshUI;
        }

        // 2. Find stats on new player
        if (newPlayer != null)
        {
            _currentStats = newPlayer.GetComponentInChildren<PlayerStats>();
        }
        else
        {
            _currentStats = null;
        }

        // 3. Subscribe to new player
        if (_currentStats != null)
        {
            _currentStats.OnSkillPointsChanged += RefreshUI;
        }

        // 4. Update UI immediately
        RefreshUI();
    }

    private void RefreshUI()
    {
        // Default to hidden if no player selected
        if (_currentStats == null)
        {
            if (attributePointsButton != null) attributePointsButton.SetActive(false);
            if (skillPointsButton != null) skillPointsButton.SetActive(false);
            return;
        }

        // --- Attribute Points Logic ---
        if (attributePointsButton != null)
        {
            bool hasStatPoints = _currentStats.unspentStatPoints > 0;
            attributePointsButton.SetActive(hasStatPoints);

            if (hasStatPoints && attributeCountText != null)
            {
                attributeCountText.text = _currentStats.unspentStatPoints.ToString();
            }
        }

        // --- Skill Points Logic ---
        if (skillPointsButton != null)
        {
            bool hasSkillPoints = _currentStats.unspentSkillPoints > 0;
            skillPointsButton.SetActive(hasSkillPoints);

            if (hasSkillPoints && skillCountText != null)
            {
                skillCountText.text = _currentStats.unspentSkillPoints.ToString();
            }
        }
    }
}