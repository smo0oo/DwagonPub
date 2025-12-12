using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class LevelUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject levelUIPanel;
    public Slider xpSlider;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpText;
    public TextMeshProUGUI unspentPointsText;

    [Header("Stat Allocation Buttons")]
    public Button addStrengthButton;
    public Button addAgilityButton;
    public Button addIntelligenceButton;
    public Button addFaithButton;

    [Header("Manager References")]
    public PartyManager partyManager;

    private PlayerStats currentPlayerStats;

    void Start()
    {
        addStrengthButton?.onClick.AddListener(() => AllocatePoint("Strength"));
        addAgilityButton?.onClick.AddListener(() => AllocatePoint("Agility"));
        addIntelligenceButton?.onClick.AddListener(() => AllocatePoint("Intelligence"));
        addFaithButton?.onClick.AddListener(() => AllocatePoint("Faith"));

        if (levelUIPanel != null)
        {
            levelUIPanel.SetActive(false);
        }

        if (partyManager != null)
        {
            partyManager.OnLevelUp += UpdateUI;
            partyManager.OnXPChanged += UpdateUI;
        }

        PlayerStats.OnStatsChanged += UpdateUI;
    }

    private void OnDestroy()
    {
        if (partyManager != null)
        {
            partyManager.OnLevelUp -= UpdateUI;
            partyManager.OnXPChanged -= UpdateUI;
        }
        PlayerStats.OnStatsChanged -= UpdateUI;
    }

    void Update()
    {
        // ADDED GUARD CLAUSE FOR MAIN MENU
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            if (levelUIPanel != null)
            {
                levelUIPanel.SetActive(!levelUIPanel.activeSelf);
            }
        }
    }

    public void DisplayLevelInfo(PlayerStats newPlayerStats)
    {
        currentPlayerStats = newPlayerStats;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (partyManager == null)
        {
            ClearUI();
            return;
        }

        if (levelText != null) levelText.text = $"Level: {partyManager.partyLevel}";
        if (xpText != null) xpText.text = $"XP: {partyManager.currentXP} / {partyManager.xpToNextLevel}";

        if (xpSlider != null)
        {
            if (partyManager.xpToNextLevel > 0)
            {
                xpSlider.DOValue((float)partyManager.currentXP / partyManager.xpToNextLevel, 0.5f);
            }
            else
            {
                xpSlider.value = 0;
            }
        }

        if (currentPlayerStats != null)
        {
            if (unspentPointsText != null) unspentPointsText.text = $"Points: {currentPlayerStats.unspentStatPoints}";
            bool hasPoints = currentPlayerStats.unspentStatPoints > 0;

            if (addStrengthButton != null)
            {
                addStrengthButton.gameObject.SetActive(true);
                addStrengthButton.interactable = hasPoints;
            }
            if (addAgilityButton != null)
            {
                addAgilityButton.gameObject.SetActive(true);
                addAgilityButton.interactable = hasPoints;
            }
            if (addIntelligenceButton != null)
            {
                addIntelligenceButton.gameObject.SetActive(true);
                addIntelligenceButton.interactable = hasPoints;
            }
            if (addFaithButton != null)
            {
                addFaithButton.gameObject.SetActive(true);
                addFaithButton.interactable = hasPoints;
            }
        }
        else
        {
            ClearIndividualUI();
        }
    }

    private void ClearUI()
    {
        if (levelText != null) levelText.text = "Level: --";
        if (xpText != null) xpText.text = "XP: -- / --";
        if (xpSlider != null) xpSlider.value = 0;
        ClearIndividualUI();
    }

    private void ClearIndividualUI()
    {
        if (unspentPointsText != null) unspentPointsText.text = "Points: 0";
        addStrengthButton?.gameObject.SetActive(false);
        addAgilityButton?.gameObject.SetActive(false);
        addIntelligenceButton?.gameObject.SetActive(false);
        addFaithButton?.gameObject.SetActive(false);
    }

    private void AllocatePoint(string statName)
    {
        if (currentPlayerStats != null)
        {
            currentPlayerStats.AllocateStatPoint(statName);
        }
    }
}