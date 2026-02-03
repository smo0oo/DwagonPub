using UnityEngine;
using TMPro;
using System;

public class StatsUIManager : MonoBehaviour
{
    public static event Action<bool> OnStatsPanelToggled;

    [Header("UI Panel")]
    public GameObject statsPanel;

    [Header("Primary Stat Texts")]
    public TextMeshProUGUI strengthText;
    public TextMeshProUGUI agilityText;
    public TextMeshProUGUI intelligenceText;
    public TextMeshProUGUI faithText;
    public TextMeshProUGUI classNameText;

    [Header("Resource Texts")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI manaText;

    [Header("Secondary Stat Texts")]
    public TextMeshProUGUI critChanceText;
    public TextMeshProUGUI spellCritChanceText;
    public TextMeshProUGUI attackSpeedText;
    public TextMeshProUGUI cooldownReductionText;
    public TextMeshProUGUI dodgeChanceText;
    public TextMeshProUGUI parryChanceText;
    public TextMeshProUGUI blockChanceText;
    public TextMeshProUGUI magicResistText;
    public TextMeshProUGUI healingBonusText;

    private PlayerStats currentPlayerStats;
    private Health currentPlayerHealth;

    void Start()
    {
        if (statsPanel != null) statsPanel.SetActive(false);
        PlayerStats.OnStatsChanged += UpdateStatTexts;
    }

    private void OnDestroy()
    {
        PlayerStats.OnStatsChanged -= UpdateStatTexts;
        if (currentPlayerHealth != null)
        {
            currentPlayerHealth.OnHealthChanged -= UpdateStatTexts;
        }
    }

    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleStatsPanel();
        }
    }

    public void ToggleStatsPanel()
    {
        if (statsPanel != null)
        {
            bool isOpening = !statsPanel.activeSelf;
            statsPanel.SetActive(isOpening);

            OnStatsPanelToggled?.Invoke(isOpening);

            if (!isOpening && TooltipManager.instance != null)
            {
                TooltipManager.instance.HideTooltip();
            }
        }
    }

    public void DisplayStatsAndClass(PlayerStats newPlayerStats, Health newPlayerHealth)
    {
        if (currentPlayerHealth != null)
        {
            currentPlayerHealth.OnHealthChanged -= UpdateStatTexts;
        }

        currentPlayerStats = newPlayerStats;
        currentPlayerHealth = newPlayerHealth;

        if (currentPlayerStats != null)
        {
            if (currentPlayerHealth != null)
            {
                currentPlayerHealth.OnHealthChanged += UpdateStatTexts;
            }
            UpdateAllTexts();
        }
        else
        {
            ClearAllTexts();
        }
    }

    private void UpdateAllTexts()
    {
        UpdateStatTexts();
        UpdateClassText();
    }

    private void ClearAllTexts()
    {
        ClearStatTexts();
        if (classNameText != null) classNameText.text = "Class: --";
    }

    private void UpdateStatTexts()
    {
        if (currentPlayerStats == null)
        {
            ClearStatTexts();
            return;
        }

        // --- Primary Stats ---
        // Note: You can add StatTooltipTrigger to these in Unity Inspector if you want generic tooltips for them.
        if (strengthText != null) strengthText.text = $"Strength: {currentPlayerStats.finalStrength}";
        if (agilityText != null) agilityText.text = $"Agility: {currentPlayerStats.finalAgility}";
        if (intelligenceText != null) intelligenceText.text = $"Intelligence: {currentPlayerStats.finalIntelligence}";
        if (faithText != null) faithText.text = $"Faith: {currentPlayerStats.finalFaith}";

        // --- Resources ---
        if (healthText != null)
        {
            if (currentPlayerHealth != null)
            {
                healthText.text = $"Health: {currentPlayerHealth.currentHealth} / {currentPlayerHealth.maxHealth}";
                SetTooltip(healthText, "Health", currentPlayerStats.secondaryStats.healthTooltip);
            }
            else
            {
                healthText.text = "Health: -- / --";
            }
        }
        if (manaText != null)
        {
            manaText.text = $"Mana: {Mathf.FloorToInt(currentPlayerStats.currentMana)} / {currentPlayerStats.maxMana}";
            SetTooltip(manaText, "Mana", currentPlayerStats.secondaryStats.manaTooltip);
        }

        // --- Secondary Stats ---
        if (critChanceText != null)
        {
            critChanceText.text = $"Critical Chance: {currentPlayerStats.secondaryStats.critChance.ToString("F1")}%";
            SetTooltip(critChanceText, "Critical Strike Chance", currentPlayerStats.secondaryStats.critChanceTooltip);
        }
        if (spellCritChanceText != null)
        {
            spellCritChanceText.text = $"Spell Critical Chance: {currentPlayerStats.secondaryStats.spellCritChance.ToString("F1")}%";
            SetTooltip(spellCritChanceText, "Spell Critical Chance", currentPlayerStats.secondaryStats.spellCritChanceTooltip);
        }
        if (attackSpeedText != null)
        {
            attackSpeedText.text = $"Attack Speed: x{currentPlayerStats.secondaryStats.attackSpeed.ToString("F2")}";
            SetTooltip(attackSpeedText, "Attack Speed", currentPlayerStats.secondaryStats.attackSpeedTooltip);
        }
        if (cooldownReductionText != null)
        {
            cooldownReductionText.text = $"Cooldown Reduction: {(1 - currentPlayerStats.secondaryStats.cooldownReduction) * 100f:F1}%";
            SetTooltip(cooldownReductionText, "Cooldown Reduction", currentPlayerStats.secondaryStats.cooldownReductionTooltip);
        }
        if (dodgeChanceText != null)
        {
            dodgeChanceText.text = $"Dodge Chance: {currentPlayerStats.secondaryStats.dodgeChance.ToString("F1")}%";
            SetTooltip(dodgeChanceText, "Dodge Chance", currentPlayerStats.secondaryStats.dodgeChanceTooltip);
        }
        if (parryChanceText != null)
        {
            parryChanceText.text = $"Parry Chance: {currentPlayerStats.secondaryStats.parryChance.ToString("F1")}%";
            SetTooltip(parryChanceText, "Parry Chance", currentPlayerStats.secondaryStats.parryChanceTooltip);
        }
        if (blockChanceText != null)
        {
            blockChanceText.text = $"Block Chance: {currentPlayerStats.secondaryStats.blockChance.ToString("F1")}%";
            SetTooltip(blockChanceText, "Block Chance", currentPlayerStats.secondaryStats.blockChanceTooltip);
        }
        if (magicResistText != null)
        {
            magicResistText.text = $"Magic Resist: {currentPlayerStats.secondaryStats.magicResistance.ToString("F1")}%";
            SetTooltip(magicResistText, "Magic Resistance", currentPlayerStats.secondaryStats.magicResistTooltip);
        }
        if (healingBonusText != null)
        {
            healingBonusText.text = $"Healing Bonus: {currentPlayerStats.secondaryStats.healingBonus.ToString("F1")}%";
            SetTooltip(healingBonusText, "Healing Bonus", currentPlayerStats.secondaryStats.healingBonusTooltip);
        }
    }

    // --- Helper to set Title & Text ---
    private void SetTooltip(TextMeshProUGUI uiElement, string title, string content)
    {
        StatTooltipTrigger trigger = uiElement.GetComponent<StatTooltipTrigger>();
        if (trigger != null)
        {
            trigger.title = title;
            trigger.tooltipText = content;
        }
    }

    private void UpdateClassText()
    {
        if (currentPlayerStats != null && classNameText != null)
        {
            if (currentPlayerStats.characterClass != null)
            {
                classNameText.text = $"Class: {currentPlayerStats.characterClass.displayName}";
            }
            else
            {
                classNameText.text = "Class: --";
            }
        }
    }

    private void ClearStatTexts()
    {
        if (strengthText != null) strengthText.text = "Strength: --";
        if (agilityText != null) agilityText.text = "Agility: --";
        if (intelligenceText != null) intelligenceText.text = "Intelligence: --";
        if (faithText != null) faithText.text = "Faith: --";
        if (healthText != null) healthText.text = "Health: -- / --";
        if (manaText != null) manaText.text = "Mana: -- / --";
        if (critChanceText != null) critChanceText.text = "Critical Chance: --";
        if (spellCritChanceText != null) spellCritChanceText.text = "Spell Critical Chance: --";
        if (attackSpeedText != null) attackSpeedText.text = "Attack Speed: --";
        if (cooldownReductionText != null) cooldownReductionText.text = "Cooldown Reduction: --";
        if (dodgeChanceText != null) dodgeChanceText.text = "Dodge Chance: --";
        if (parryChanceText != null) parryChanceText.text = "Parry Chance: --";
        if (blockChanceText != null) blockChanceText.text = "Block Chance: --";
        if (magicResistText != null) magicResistText.text = "Magic Resist: --";
        if (healingBonusText != null) healingBonusText.text = "Healing Bonus: --";
    }
}