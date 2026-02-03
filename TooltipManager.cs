using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
using System.Text;
using System.Linq;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager instance;

    [Header("Main Containers")]
    public GameObject tooltipPanel;
    public RectTransform tooltipRect;

    [Header("Header Section")]
    public TextMeshProUGUI tooltipNameText;
    public Image nameBackgroundImage;

    [Header("Content Sections")]
    public TextMeshProUGUI tooltipRarityText;

    [Header("Stats Section")]
    public GameObject spacer1;
    public TextMeshProUGUI tooltipStatsText;

    [Header("Description Section")]
    public GameObject spacer2;
    public TextMeshProUGUI tooltipDescriptionText;

    [Header("Settings")]
    public bool colorNameBackgroundByRarity = true;

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
        if (instance != null && instance != this) Destroy(gameObject);
        else instance = this;

        if (tooltipPanel != null)
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
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
        if (tooltipPanel != null && tooltipPanel.activeSelf) UpdateTooltipPosition();
    }

    private void ClearTooltipUI()
    {
        tooltipNameText.text = "";
        tooltipRarityText.text = "";
        tooltipStatsText.text = "";
        tooltipDescriptionText.text = "";

        if (tooltipRarityText) tooltipRarityText.gameObject.SetActive(false);
        if (tooltipStatsText) tooltipStatsText.gameObject.SetActive(false);
        if (tooltipDescriptionText) tooltipDescriptionText.gameObject.SetActive(false);

        if (spacer1) spacer1.SetActive(false);
        if (spacer2) spacer2.SetActive(false);
    }

    public void ShowAbilityTooltip(Ability currentRankAbility, Ability nextRankAbility)
    {
        ClearTooltipUI();
        Ability ability = currentRankAbility ?? nextRankAbility;
        if (ability == null) { HideTooltip(); return; }

        // Header
        tooltipNameText.text = ability.abilityName;
        tooltipNameText.color = Color.white;
        if (nameBackgroundImage) nameBackgroundImage.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);

        // Stats
        StringBuilder sbStats = new StringBuilder();
        void AppendStats(Ability a, string prefix)
        {
            if (a.abilityType == AbilityType.ChanneledBeam)
            {
                if (a.manaDrain > 0) sbStats.AppendLine($"{prefix}<color=#8888FF>Mana: {a.manaDrain}/sec</color>");
                sbStats.AppendLine($"{prefix}<color=#DDDDDD>Tick Rate: {a.tickRate}s</color>");
            }
            else
            {
                if (a.manaCost > 0) sbStats.AppendLine($"{prefix}<color=#8888FF>Mana: {a.manaCost}</color>");
                if (a.cooldown > 0) sbStats.AppendLine($"{prefix}<color=#DDDDDD>Cooldown: {a.cooldown}s</color>");
                if (a.castTime > 0) sbStats.AppendLine($"{prefix}<color=#DDDDDD>Cast Time: {a.castTime}s</color>");
            }
        }

        if (nextRankAbility == null)
        {
            AppendStats(currentRankAbility, "");
        }
        else
        {
            if (currentRankAbility != null)
            {
                sbStats.AppendLine("<color=#a0a0a0><size=80%>Current Rank:</size></color>");
                if (currentRankAbility.abilityType == AbilityType.ChanneledBeam)
                    sbStats.AppendLine($"<color=#999999>Drain: {currentRankAbility.manaDrain}/s</color>");
                else
                    sbStats.AppendLine($"<color=#999999>Mana: {currentRankAbility.manaCost} | CD: {currentRankAbility.cooldown}s</color>");
            }
            else
            {
                sbStats.AppendLine("<color=#888888>Not Learned</color>");
            }
            sbStats.AppendLine("\n<color=#FFFFFF>Next Rank:</color>");
            AppendStats(nextRankAbility, "");
        }

        if (sbStats.Length > 0)
        {
            tooltipStatsText.text = sbStats.ToString();
            tooltipStatsText.gameObject.SetActive(true);
            if (spacer1) spacer1.SetActive(true);
        }

        // Description
        StringBuilder sbDesc = new StringBuilder();
        void AppendEffects(Ability a)
        {
            bool hasHostile = a.hostileEffects.Count > 0;
            bool hasFriendly = a.friendlyEffects.Count > 0;
            if (hasHostile || hasFriendly) sbDesc.AppendLine();
            foreach (var effect in a.hostileEffects) if (effect != null) sbDesc.AppendLine($"<color=#FF8888>• {effect.GetEffectDescription()}</color>");
            foreach (var effect in a.friendlyEffects) if (effect != null) sbDesc.AppendLine($"<color=#88FF88>• {effect.GetEffectDescription()}</color>");
        }

        if (nextRankAbility == null)
        {
            sbDesc.Append(currentRankAbility.description);
            AppendEffects(currentRankAbility);
        }
        else
        {
            sbDesc.Append(nextRankAbility.description);
            AppendEffects(nextRankAbility);
        }

        tooltipDescriptionText.text = sbDesc.ToString();
        tooltipDescriptionText.gameObject.SetActive(true);

        if (tooltipStatsText.gameObject.activeSelf && spacer2) spacer2.SetActive(true);

        AnimateTooltip(true);
    }

    public void ShowItemTooltip(ItemStack itemStack, bool requirementsMet, PlayerStats viewerStats)
    {
        if (itemStack == null || itemStack.itemData == null) { HideTooltip(); return; }
        ClearTooltipUI();

        // Header
        tooltipNameText.text = itemStack.itemData.displayName;
        tooltipNameText.color = requirementsMet ? Color.white : requirementsNotMetColor;

        ItemStats stats = itemStack.itemData.stats;
        Color rarityCol = commonColor;
        if (stats != null)
        {
            rarityCol = GetRarityColor(stats.rarity);
            tooltipRarityText.text = stats.rarity.ToString();
            tooltipRarityText.color = rarityCol;
            tooltipRarityText.gameObject.SetActive(true);
            if (spacer1) spacer1.SetActive(true);
        }
        if (nameBackgroundImage)
        {
            if (colorNameBackgroundByRarity) nameBackgroundImage.color = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.7f);
            else nameBackgroundImage.color = new Color(0, 0, 0, 0.8f);
        }

        // Stats
        StringBuilder sbStats = new StringBuilder();
        if (viewerStats != null && stats is ItemWeaponStats weaponStats)
        {
            if (weaponStats.weaponCategory != ItemWeaponStats.WeaponCategory.Wand && weaponStats.weaponCategory != ItemWeaponStats.WeaponCategory.Staff)
            {
                float dps = viewerStats.CalculateWeaponDps(weaponStats);
                sbStats.AppendLine($"<color=#FFD700><size=120%>{dps:F1} DPS</size></color>");
            }
            else
            {
                float spellPower = viewerStats.secondaryStats.magicAttackDamage;
                sbStats.AppendLine($"<color=#9090ff>+{spellPower:F1} Spell Power</color>");
            }
        }
        if (stats != null)
        {
            string statDesc = stats.GetStatsDescription();
            if (!string.IsNullOrEmpty(statDesc))
            {
                if (sbStats.Length > 0) sbStats.AppendLine();
                sbStats.Append(statDesc);
            }
        }
        bool hasLevelReq = itemStack.itemData.levelRequirement > 1;
        bool hasClassReq = itemStack.itemData.allowedClasses.Count > 0;
        if (hasLevelReq || hasClassReq)
        {
            sbStats.AppendLine();
            if (hasLevelReq) sbStats.AppendLine($"<color=#{(requirementsMet ? "FFFFFF" : "FF0000")}>Requires Level {itemStack.itemData.levelRequirement}</color>");
            if (hasClassReq)
            {
                string classList = string.Join(", ", itemStack.itemData.allowedClasses.Select(c => c.displayName));
                sbStats.AppendLine($"<color=#{(requirementsMet ? "FFFFFF" : "FF0000")}>Requires: {classList}</color>");
            }
        }

        if (sbStats.Length > 0)
        {
            tooltipStatsText.text = sbStats.ToString();
            tooltipStatsText.gameObject.SetActive(true);
            if (spacer2) spacer2.SetActive(true);
        }

        // Description
        StringBuilder sbDesc = new StringBuilder();
        sbDesc.Append(itemStack.itemData.description);
        if (itemStack.itemData.itemValue > 0)
        {
            sbDesc.Append($"\n\n<color=#FFD700>Value: {itemStack.itemData.itemValue}</color>");
            if (itemStack.quantity > 1) sbDesc.Append($" <color=#AAAAAA>({itemStack.itemData.itemValue * itemStack.quantity})</color>");
        }

        tooltipDescriptionText.text = sbDesc.ToString();
        tooltipDescriptionText.gameObject.SetActive(true);

        AnimateTooltip(true);
    }

    // --- REFACTORED: Simple Tooltip with AAA Spacing ---
    public void ShowSimpleTooltip(string title, string description)
    {
        ClearTooltipUI();

        // Header
        tooltipNameText.text = title;
        tooltipNameText.color = Color.white;
        if (nameBackgroundImage) nameBackgroundImage.color = new Color(0, 0, 0, 0.8f);

        // Description
        tooltipDescriptionText.text = description;
        tooltipDescriptionText.gameObject.SetActive(true);

        // Spacer Logic: If we have a title, use spacer1 to separate it from description
        // (Since Stats Text is empty/inactive, spacer1 acts as the divider between Header and Description)
        if (!string.IsNullOrEmpty(title) && spacer1)
        {
            spacer1.SetActive(true);
        }

        AnimateTooltip(true);
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf) AnimateTooltip(false);
    }

    private void AnimateTooltip(bool show)
    {
        tooltipPanel.transform.DOKill();
        if (show)
        {
            tooltipPanel.transform.localScale = new Vector3(startScale, startScale, 1f);
            tooltipPanel.SetActive(true);
            tooltipPanel.transform.DOScale(1f, openDuration).SetEase(openEase);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        }
        else
        {
            tooltipPanel.transform.DOScale(0f, closeDuration).SetEase(closeEase)
                .OnComplete(() => tooltipPanel.SetActive(false));
        }
    }

    private void UpdateTooltipPosition()
    {
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