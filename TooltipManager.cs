using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Text;
using System.Linq;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager instance;

    [Header("UI References")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipNameText;
    public TextMeshProUGUI tooltipRarityText;
    public TextMeshProUGUI tooltipDescriptionText;

    [Header("Rarity Colors")]
    public Color commonColor = Color.white;
    public Color uncommonColor = Color.green;
    public Color rareColor = Color.blue;
    public Color epicColor = new Color(0.5f, 0f, 0.5f);
    public Color legendaryColor = new Color(1f, 0.84f, 0f);
    public Color requirementsNotMetColor = Color.red;

    [Header("Animation Settings")]
    public float startScale = 0.8f;
    public float openDuration = 0.15f;
    public float closeDuration = 0.1f;
    public Ease openEase = Ease.OutBack;
    public Ease closeEase = Ease.InBack;

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
        if (tooltipPanel != null)
        {
            tooltipPanel.transform.localScale = Vector3.zero;
            tooltipPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdateTooltipPosition();
        }
    }

    public void ShowAbilityTooltip(Ability currentRankAbility, Ability nextRankAbility)
    {
        Ability abilityForTitle = currentRankAbility ?? nextRankAbility;
        if (abilityForTitle == null)
        {
            HideTooltip();
            return;
        }

        tooltipNameText.text = abilityForTitle.abilityName;
        tooltipNameText.color = Color.white;
        tooltipRarityText.gameObject.SetActive(false);

        StringBuilder sb = new StringBuilder();

        if (nextRankAbility == null)
        {
            sb.AppendLine(currentRankAbility.description);
            if (currentRankAbility.manaCost > 0) sb.AppendLine($"\nMana Cost: {currentRankAbility.manaCost}");
            if (currentRankAbility.cooldown > 0) sb.AppendLine($"Cooldown: {currentRankAbility.cooldown}s");
        }
        else
        {
            if (currentRankAbility != null)
            {
                sb.AppendLine("Current Rank:");
                sb.Append("<color=#a0a0a0>");
                sb.AppendLine(currentRankAbility.description);
                if (currentRankAbility.manaCost > 0) sb.AppendLine($"Mana Cost: {currentRankAbility.manaCost}");
                if (currentRankAbility.cooldown > 0) sb.AppendLine($"Cooldown: {currentRankAbility.cooldown}s");
                sb.Append("</color>");
            }
            else
            {
                sb.AppendLine("Not yet learned.");
            }

            sb.AppendLine("\n<color=#FFFFFF>Next Rank:</color>");
            sb.AppendLine(nextRankAbility.description);

            if (nextRankAbility.manaCost > 0)
            {
                string manaColor = (currentRankAbility == null || nextRankAbility.manaCost == currentRankAbility.manaCost) ? "FFFFFF" : (nextRankAbility.manaCost < currentRankAbility.manaCost ? "00FF00" : "FF0000");
                sb.Append($"<color=#{manaColor}>Mana Cost: {nextRankAbility.manaCost}</color>");
            }
            if (nextRankAbility.cooldown > 0)
            {
                string cdColor = (currentRankAbility == null || nextRankAbility.cooldown == currentRankAbility.cooldown) ? "FFFFFF" : (nextRankAbility.cooldown < currentRankAbility.cooldown ? "00FF00" : "FF0000");
                sb.Append($"\n<color=#{cdColor}>Cooldown: {nextRankAbility.cooldown}s</color>");
            }
        }

        tooltipDescriptionText.text = sb.ToString();
        AnimateTooltip(true);
    }

    // --- REFACTORED: Now accepts PlayerStats to calculate DPS ---
    public void ShowItemTooltip(ItemStack itemStack, bool requirementsMet, PlayerStats viewerStats)
    {
        if (itemStack == null || itemStack.itemData == null)
        {
            HideTooltip();
            return;
        }

        tooltipNameText.text = itemStack.itemData.displayName;
        tooltipNameText.color = requirementsMet ? Color.white : requirementsNotMetColor;

        ItemStats stats = itemStack.itemData.stats;
        if (stats != null)
        {
            tooltipRarityText.gameObject.SetActive(true);
            tooltipRarityText.text = stats.rarity.ToString();
            tooltipRarityText.color = GetRarityColor(stats.rarity);
        }
        else
        {
            tooltipRarityText.gameObject.SetActive(false);
        }

        StringBuilder sb = new StringBuilder();

        // --- NEW: DPS Calculation and Display ---
        if (viewerStats != null && stats is ItemWeaponStats weaponStats)
        {
            // For physical weapons, show DPS
            if (weaponStats.weaponCategory != ItemWeaponStats.WeaponCategory.Wand && weaponStats.weaponCategory != ItemWeaponStats.WeaponCategory.Staff)
            {
                float dps = viewerStats.CalculateWeaponDps(weaponStats);
                sb.AppendLine($"<color=#c0c0c0>{dps:F1} Damage Per Second</color>");
            }
            // For caster weapons, show Bonus Spell Damage instead
            else
            {
                float spellPower = viewerStats.secondaryStats.magicAttackDamage;
                sb.AppendLine($"<color=#9090ff>+{spellPower:F1} Bonus Spell Damage</color>");
            }
        }

        sb.Append(itemStack.itemData.description);

        if (stats != null)
        {
            sb.Append("\n\n").Append(stats.GetStatsDescription());
        }

        if (itemStack.itemData.itemValue > 0)
        {
            sb.Append($"\n\nValue: {itemStack.itemData.itemValue}");
            if (itemStack.quantity > 1)
            {
                sb.Append($" (Stack Value: {itemStack.itemData.itemValue * itemStack.quantity})");
            }
        }

        bool hasLevelReq = itemStack.itemData.levelRequirement > 1;
        bool hasClassReq = itemStack.itemData.allowedClasses.Count > 0;

        if (hasLevelReq || hasClassReq)
        {
            sb.Append("\n");
            if (hasLevelReq)
            {
                sb.Append($"\n<color=#{(requirementsMet ? "FFFFFF" : "FF0000")}>Requires Level: {itemStack.itemData.levelRequirement}</color>");
            }
            if (hasClassReq)
            {
                string classList = string.Join(", ", itemStack.itemData.allowedClasses.Select(c => c.displayName));
                sb.Append($"\n<color=#{(requirementsMet ? "FFFFFF" : "FF0000")}>Class: {classList}</color>");
            }
        }

        tooltipDescriptionText.text = sb.ToString();

        AnimateTooltip(true);
    }

    public void ShowSimpleTooltip(string title, string description)
    {
        if (tooltipPanel == null) return;
        tooltipNameText.text = title;
        tooltipNameText.color = Color.white;
        tooltipRarityText.gameObject.SetActive(false);
        tooltipDescriptionText.text = description;
        AnimateTooltip(true);
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            AnimateTooltip(false);
        }
    }

    private void AnimateTooltip(bool show)
    {
        tooltipPanel.transform.DOKill();

        if (show)
        {
            tooltipPanel.transform.localScale = new Vector3(startScale, startScale, 1f);
            tooltipPanel.SetActive(true);
            tooltipPanel.transform.DOScale(1f, openDuration).SetEase(openEase);
        }
        else
        {
            tooltipPanel.transform.DOScale(0f, closeDuration).SetEase(closeEase)
                .OnComplete(() => {
                    tooltipPanel.SetActive(false);
                });
        }
    }

    private void UpdateTooltipPosition()
    {
        RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        Vector2 mousePosition = Input.mousePosition;
        tooltipRect.pivot = new Vector2(mousePosition.x / Screen.width > 0.5f ? 1 : 0, mousePosition.y / Screen.height > 0.5f ? 1 : 0);
        tooltipRect.position = mousePosition;
    }

    private Color GetRarityColor(ItemStats.Rarity rarity)
    {
        switch (rarity)
        {
            case ItemStats.Rarity.Uncommon: return uncommonColor;
            case ItemStats.Rarity.Rare: return rareColor;
            case ItemStats.Rarity.Epic: return epicColor;
            case ItemStats.Rarity.Legendary: return legendaryColor;
            default: return commonColor;
        }
    }
}