using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class PlayerStats : MonoBehaviour, ISerializationCallbackReceiver
{
    [Header("Default Abilities")]
    [Tooltip("Abilities in this list will be automatically known by this character at Rank 1 when the game starts.")]
    public List<Ability> defaultKnownAbilities = new List<Ability>();

    public static event Action OnStatsChanged;
    public event Action OnManaChanged;
    public event Action OnSkillPointsChanged;
    public event Action OnAbilitiesChanged;

    [Header("Class & Skills")]
    public PlayerClass characterClass;
    public int unspentSkillPoints = 0;

    [Header("Individual Stats")]
    public int unspentStatPoints = 0;
    public int bonusStrength = 0;
    public int bonusAgility = 0;
    public int bonusIntelligence = 0;
    public int bonusFaith = 0;

    [Header("Primary Stats (Calculated)")]
    public int finalStrength;
    public int finalAgility;
    public int finalIntelligence;
    public int finalFaith;

    [Header("Secondary Stats (Calculated)")]
    public SecondaryStats secondaryStats = new SecondaryStats();

    [Header("Resources")]
    public float currentMana;
    public int maxMana;

    [Header("Mana Regen Settings")]
    [Tooltip("Base mana regenerated per second (before Intelligence scaling).")]
    public float baseManaRegen = 1.0f;

    [Tooltip("Extra mana regen added per point of Intelligence.")]
    public float intelligenceRegenScaling = 0.2f;

    [Tooltip("How often (in frames) to apply regeneration. Higher = Better Performance, Lower = Smoother UI.")]
    [Range(1, 60)]
    public int regenTickInterval = 8;

    [SerializeField, ReadOnlyInspector] private float calculatedManaRegen;
    private float accumulatedRegenTime = 0f;

    [SerializeField]
    private List<Ability> unlockedAbilityBase = new List<Ability>();
    [SerializeField]
    private List<int> unlockedAbilityRank = new List<int>();
    public Dictionary<Ability, int> unlockedAbilityRanks = new Dictionary<Ability, int>();
    public List<Ability> knownAbilities = new List<Ability>();

    private PlayerEquipment playerEquipment;
    private Health playerHealth;
    private PartyManager partyManager;

    public int currentLevel => partyManager != null ? partyManager.partyLevel : 1;

    void Awake()
    {
        OnAfterDeserialize();

        CharacterRoot root = GetComponentInParent<CharacterRoot>();
        if (root != null)
        {
            playerEquipment = root.PlayerEquipment;
            playerHealth = root.Health;
        }
        else
        {
            Debug.LogError("PlayerStats could not find CharacterRoot on its parent hierarchy!", this);
        }

        partyManager = FindFirstObjectByType<PartyManager>();
    }

    void Start()
    {
        GrantDefaultAbilities();

        CalculateFinalStats();
        if (playerHealth != null)
        {
            playerHealth.SetToMaxHealth();
        }
        UpdateKnownAbilities();
        currentMana = maxMana;
        OnManaChanged?.Invoke();
    }

    void Update()
    {
        accumulatedRegenTime += Time.deltaTime;

        if (Time.frameCount % regenTickInterval == 0)
        {
            if (currentMana < maxMana && calculatedManaRegen > 0)
            {
                RestoreMana(calculatedManaRegen * accumulatedRegenTime);
            }

            accumulatedRegenTime = 0f;
        }
    }

    private void GrantDefaultAbilities()
    {
        if (characterClass == null || characterClass.classSkillTree == null) return;

        foreach (Ability defaultAbility in defaultKnownAbilities)
        {
            if (defaultAbility == null) continue;

            SkillNode associatedNode = characterClass.classSkillTree.skillNodes
                .FirstOrDefault(node => node.skillRanks.Count > 0 && node.skillRanks[0] == defaultAbility);

            if (associatedNode != null)
            {
                Ability baseAbility = associatedNode.skillRanks.FirstOrDefault();
                if (baseAbility != null)
                {
                    if (!unlockedAbilityRanks.ContainsKey(baseAbility))
                    {
                        unlockedAbilityRanks[baseAbility] = 1;
                    }
                }
            }
        }
    }

    public void OnBeforeSerialize() { unlockedAbilityBase.Clear(); unlockedAbilityRank.Clear(); foreach (var kvp in unlockedAbilityRanks) { unlockedAbilityBase.Add(kvp.Key); unlockedAbilityRank.Add(kvp.Value); } }
    public void OnAfterDeserialize() { unlockedAbilityRanks = new Dictionary<Ability, int>(); for (int i = 0; i < unlockedAbilityBase.Count; i++) { if (unlockedAbilityBase[i] != null && !unlockedAbilityRanks.ContainsKey(unlockedAbilityBase[i])) { unlockedAbilityRanks.Add(unlockedAbilityBase[i], unlockedAbilityRank[i]); } } }

    public void SpendMana(float amount) { currentMana -= amount; if (currentMana < 0) currentMana = 0; OnManaChanged?.Invoke(); }

    public float CalculateWeaponDps(ItemWeaponStats weaponStats) { if (weaponStats == null || weaponStats.baseAttackTime <= 0) { return 0f; } float avgWeaponDamage = (weaponStats.DamageLow + weaponStats.DamageHigh) / 2f; float avgHitDamage = avgWeaponDamage + secondaryStats.damageMultiplier; float critModifiedDamage = avgHitDamage * (1 + (secondaryStats.critChance / 100f)); float finalAttackTime = weaponStats.baseAttackTime / secondaryStats.attackSpeed; return critModifiedDamage / finalAttackTime; }

    public void AddSkillPoints(int amount) { unspentSkillPoints += amount; OnSkillPointsChanged?.Invoke(); }

    public void LearnSkill(SkillNode skillToLearn)
    {
        if (skillToLearn == null || skillToLearn.skillRanks.Count == 0) return;

        Ability baseAbility = skillToLearn.skillRanks[0];

        // --- NEW: Check if this is the very first time we are unlocking this ability ---
        bool isNewUnlock = !unlockedAbilityRanks.ContainsKey(baseAbility) || unlockedAbilityRanks[baseAbility] == 0;

        unlockedAbilityRanks.TryGetValue(baseAbility, out int currentRank);
        unspentSkillPoints--;
        unlockedAbilityRanks[baseAbility] = currentRank + 1;

        UpdateKnownAbilities();
        OnSkillPointsChanged?.Invoke();

        // --- NEW: Trigger the Auto-Assign if it's a new unlock ---
        if (isNewUnlock && HotbarManager.instance != null)
        {
            HotbarManager.instance.AutoAssignAbility(baseAbility);
        }
    }

    private void UpdateKnownAbilities() { knownAbilities.Clear(); if (characterClass == null || characterClass.classSkillTree == null) return; foreach (var skillNode in characterClass.classSkillTree.skillNodes) { if (skillNode.skillRanks.Count == 0) continue; Ability baseAbility = skillNode.skillRanks[0]; if (unlockedAbilityRanks.TryGetValue(baseAbility, out int currentRank)) { for (int i = 0; i < currentRank; i++) { if (i < skillNode.skillRanks.Count) { knownAbilities.Add(skillNode.skillRanks[i]); } } } } OnAbilitiesChanged?.Invoke(); }

    public void AllocateStatPoint(string statName) { if (unspentStatPoints <= 0) return; switch (statName) { case "Strength": bonusStrength++; break; case "Agility": bonusAgility++; break; case "Intelligence": bonusIntelligence++; break; case "Faith": bonusFaith++; break; } unspentStatPoints--; CalculateFinalStats(); }

    public float RestoreMana(float amount)
    {
        float manaBeforeRestore = currentMana;
        currentMana += amount;
        if (currentMana > maxMana) currentMana = maxMana;
        OnManaChanged?.Invoke();
        return currentMana - manaBeforeRestore;
    }

    public void CalculateFinalStats()
    {
        int baseStr = characterClass != null ? characterClass.strength : 0;
        int baseAgi = characterClass != null ? characterClass.agility : 0;
        int baseInt = characterClass != null ? characterClass.intelligence : 0;
        int baseFai = characterClass != null ? characterClass.faith : 0;

        int strengthBeforeGear = baseStr + bonusStrength;
        int agilityBeforeGear = baseAgi + bonusAgility;
        int intelligenceBeforeGear = baseInt + bonusIntelligence;
        int faithBeforeGear = baseFai + bonusFaith;

        finalStrength = strengthBeforeGear;
        finalAgility = agilityBeforeGear;
        finalIntelligence = intelligenceBeforeGear;
        finalFaith = faithBeforeGear;

        ApplyEquipmentPrimaryStats();
        CalculateSecondaryRatings(strengthBeforeGear, agilityBeforeGear, intelligenceBeforeGear, faithBeforeGear);
        ConvertRatingsToPercentages();
        UpdateResources();
        GenerateTooltips(strengthBeforeGear, agilityBeforeGear, intelligenceBeforeGear, faithBeforeGear);

        OnStatsChanged?.Invoke();
        OnManaChanged?.Invoke();
    }

    private void ApplyEquipmentPrimaryStats() { if (playerEquipment == null) return; foreach (ItemStack item in playerEquipment.equippedItems.Values) { if (item == null) continue; if (item.itemData.stats is ItemArmourStats armour) { finalStrength += armour.strengthBonus; finalAgility += armour.agilityBonus; finalIntelligence += armour.intelligenceBonus; finalFaith += armour.faithBonus; } else if (item.itemData.stats is ItemWeaponStats weapon) { finalStrength += weapon.strengthBonus; finalAgility += weapon.agilityBonus; finalIntelligence += weapon.intelligenceBonus; finalFaith += weapon.faithBonus; } else if (item.itemData.stats is ItemTrinketStats trinket) { finalStrength += trinket.strengthBonus; finalAgility += trinket.agilityBonus; finalIntelligence += trinket.intelligenceBonus; finalFaith += trinket.faithBonus; } } }

    private void CalculateSecondaryRatings(int strBeforeGear, int agiBeforeGear, int intBeforeGear, int faiBeforeGear) { float strRate = partyManager != null ? partyManager.strengthConversionRate : 1f; float agiRate = partyManager != null ? partyManager.agilityConversionRate : 1f; float intRate = partyManager != null ? partyManager.intelligenceConversionRate : 1f; float faiRate = partyManager != null ? partyManager.faithConversionRate : 1f; float armRate = partyManager != null ? partyManager.armorResistanceConversionRate : 0.25f; secondaryStats.damageRating = (int)(finalStrength * strRate); secondaryStats.blockRating = (int)(finalStrength * strRate); secondaryStats.parryRating = (int)(finalStrength * strRate); secondaryStats.critRating = (int)(finalAgility * agiRate); secondaryStats.dodgeRating = (int)(finalAgility * agiRate); secondaryStats.hasteRating = (int)(finalAgility * agiRate); secondaryStats.spellCritRating = (int)(finalIntelligence * intRate); secondaryStats.cooldownReductionRating = (int)(finalIntelligence * intRate); secondaryStats.healingBonusRating = (int)(finalFaith * faiRate); secondaryStats.magicAttackRating = (int)(finalFaith * faiRate); secondaryStats.physicalResistanceRating = 0; secondaryStats.magicResistanceRating = 0; if (playerEquipment == null) return; foreach (ItemStack item in playerEquipment.equippedItems.Values) { if (item == null) continue; if (item.itemData.stats is ItemArmourStats armour) { secondaryStats.physicalResistanceRating += (int)(armour.armourPhysRes * armRate); secondaryStats.magicResistanceRating += (int)(armour.armourMagRes * armRate); secondaryStats.blockRating += armour.blockRatingBonus; secondaryStats.parryRating += armour.parryRatingBonus; secondaryStats.critRating += armour.critRatingBonus; secondaryStats.dodgeRating += armour.dodgeRatingBonus; } else if (item.itemData.stats is ItemWeaponStats weapon) { secondaryStats.critRating += weapon.critRatingBonus; secondaryStats.hasteRating += weapon.hasteRatingBonus; } } }

    private void ConvertRatingsToPercentages() { int playerLevel = partyManager != null ? partyManager.partyLevel : 1; float ratingDivisor = (partyManager != null ? partyManager.ratingToPercentDivisor : 50f) * playerLevel; if (ratingDivisor <= 0) ratingDivisor = 1f; float damageRoot = partyManager != null ? partyManager.directDamageLevelRoot : 2f; if (damageRoot <= 0) damageRoot = 1f; float damageLevelDivisor = Mathf.Pow(playerLevel, 1f / damageRoot); if (damageLevelDivisor <= 0) damageLevelDivisor = 1f; secondaryStats.damageMultiplier = secondaryStats.damageRating / damageLevelDivisor; secondaryStats.magicAttackDamage = secondaryStats.magicAttackRating / damageLevelDivisor; secondaryStats.blockChance = secondaryStats.blockRating / ratingDivisor * 100f; secondaryStats.parryChance = secondaryStats.parryRating / ratingDivisor * 100f; secondaryStats.critChance = secondaryStats.critRating / ratingDivisor * 100f; secondaryStats.dodgeChance = secondaryStats.dodgeRating / ratingDivisor * 100f; secondaryStats.attackSpeed = 1.0f + ((secondaryStats.hasteRating / ratingDivisor * 100f) / 100f); secondaryStats.spellCritChance = secondaryStats.spellCritRating / ratingDivisor * 100f; secondaryStats.cooldownReduction = 1.0f - ((secondaryStats.cooldownReductionRating / ratingDivisor * 100f) / 100f); secondaryStats.healingBonus = secondaryStats.healingBonusRating * 0.5f; secondaryStats.magicResistance = secondaryStats.magicResistanceRating / ratingDivisor * 100f; secondaryStats.physicalResistance = secondaryStats.physicalResistanceRating / ratingDivisor * 100f; }

    private void UpdateResources()
    {
        if (playerHealth != null) { playerHealth.UpdateMaxHealth(100 + (finalStrength * 10)); }
        maxMana = 50 + (finalFaith * 10);

        calculatedManaRegen = baseManaRegen + (finalIntelligence * intelligenceRegenScaling);

        if (currentMana > maxMana) currentMana = maxMana;
    }

    private void GenerateTooltips(int strBeforeGear, int agiBeforeGear, int intBeforeGear, int faiBeforeGear)
    {
        int playerLevel = partyManager != null ? partyManager.partyLevel : 1;
        float ratingDivisor = (partyManager != null ? partyManager.ratingToPercentDivisor : 50f) * playerLevel;
        if (ratingDivisor <= 0) ratingDivisor = 1;
        float strRate = partyManager != null ? partyManager.strengthConversionRate : 1f;
        float agiRate = partyManager != null ? partyManager.agilityConversionRate : 1f;
        float intRate = partyManager != null ? partyManager.intelligenceConversionRate : 1f;
        var sb = new StringBuilder();
        string conversionText = $"Conversion: {ratingDivisor:F0} rating = 1%";
        int bonusCritFromGear = secondaryStats.critRating - (int)(finalAgility * agiRate);
        sb.Clear().AppendLine($"{secondaryStats.critChance:F2}% Critical Strike Chance").AppendLine("--------------------").AppendLine($"From Agility: {(int)(agiBeforeGear * agiRate)} rating").AppendLine($"From Gear (Primary): {(int)((finalAgility - agiBeforeGear) * agiRate)} rating").AppendLine($"From Gear (Secondary): {bonusCritFromGear} rating").AppendLine("--------------------").AppendLine($"Total Rating: {secondaryStats.critRating}").Append(conversionText);
        secondaryStats.critChanceTooltip = sb.ToString();

        sb.Clear().AppendLine($"{secondaryStats.spellCritChance:F2}% Spell Critical Chance").AppendLine("--------------------").AppendLine($"From Intelligence: {(int)(intBeforeGear * intRate)} rating").AppendLine($"From Gear (Primary): {(int)((finalIntelligence - intBeforeGear) * intRate)} rating").AppendLine("--------------------").AppendLine($"Total Rating: {secondaryStats.spellCritRating}").Append(conversionText);
        secondaryStats.spellCritChanceTooltip = sb.ToString();

        int bonusDodgeFromGear = secondaryStats.dodgeRating - (int)(finalAgility * agiRate);
        sb.Clear().AppendLine($"{secondaryStats.dodgeChance:F2}% Dodge Chance").AppendLine("--------------------").AppendLine($"From Agility: {(int)(agiBeforeGear * agiRate)} rating").AppendLine($"From Gear (Primary): {(int)((finalAgility - agiBeforeGear) * agiRate)} rating").AppendLine($"From Gear (Secondary): {bonusDodgeFromGear} rating").AppendLine("--------------------").AppendLine($"Total Rating: {secondaryStats.dodgeRating}").Append(conversionText);
        secondaryStats.dodgeChanceTooltip = sb.ToString();

        int bonusParryFromGear = secondaryStats.parryRating - (int)(finalStrength * strRate);
        sb.Clear().AppendLine($"{secondaryStats.parryChance:F2}% Parry Chance").AppendLine("--------------------").AppendLine($"From Strength: {(int)(strBeforeGear * strRate)} rating").AppendLine($"From Gear (Primary): {(int)((finalStrength - strBeforeGear) * strRate)} rating").AppendLine($"From Gear (Secondary): {bonusParryFromGear} rating").AppendLine("--------------------").AppendLine($"Total Rating: {secondaryStats.parryRating}").Append(conversionText);
        secondaryStats.parryChanceTooltip = sb.ToString();

        int bonusBlockFromGear = secondaryStats.blockRating - (int)(finalStrength * strRate);
        sb.Clear().AppendLine($"{secondaryStats.blockChance:F2}% Block Chance").AppendLine("--------------------").AppendLine($"From Strength: {(int)(strBeforeGear * strRate)} rating").AppendLine($"From Gear (Primary): {(int)((finalStrength - strBeforeGear) * strRate)} rating").AppendLine($"From Gear (Secondary): {bonusBlockFromGear} rating").AppendLine("--------------------").AppendLine($"Total Rating: {secondaryStats.blockRating}").Append(conversionText);
        secondaryStats.blockChanceTooltip = sb.ToString();

        int bonusHasteFromGear = secondaryStats.hasteRating - (int)(finalAgility * agiRate);
        sb.Clear().AppendLine($"{(secondaryStats.attackSpeed - 1.0f) * 100f:F2}% increased Attack Speed").AppendLine("--------------------").AppendLine($"From Agility: {(int)(agiBeforeGear * agiRate)} rating").AppendLine($"From Gear (Primary): {(int)((finalAgility - agiBeforeGear) * agiRate)} rating").AppendLine($"From Gear (Secondary): {bonusHasteFromGear} rating").AppendLine("--------------------").AppendLine($"Total Haste Rating: {secondaryStats.hasteRating}").Append(conversionText);
        secondaryStats.attackSpeedTooltip = sb.ToString();

        sb.Clear().AppendLine($"{(1.0f - secondaryStats.cooldownReduction) * 100f:F2}% Cooldown Reduction").AppendLine("--------------------").AppendLine($"From Intelligence: {(int)(intBeforeGear * intRate)} rating").AppendLine($"From Gear (Primary): {(int)((finalIntelligence - intBeforeGear) * intRate)} rating").AppendLine("--------------------").AppendLine($"Total Rating: {secondaryStats.cooldownReductionRating}").Append(conversionText);
        secondaryStats.cooldownReductionTooltip = sb.ToString();

        sb.Clear().AppendLine($"{secondaryStats.magicResistance:F2}% Magic Resistance").AppendLine("--------------------").AppendLine($"Total Rating: {secondaryStats.magicResistanceRating}").Append(conversionText);
        secondaryStats.magicResistTooltip = sb.ToString();

        sb.Clear().AppendLine($"{secondaryStats.healingBonus:F2}% Healing Bonus").AppendLine("--------------------").AppendLine($"Total Healing Rating: {secondaryStats.healingBonusRating}").Append("Conversion: 2 rating = 1% bonus");
        secondaryStats.healingBonusTooltip = sb.ToString();

        if (playerHealth != null)
        {
            sb.Clear().AppendLine($"<color=green>Health: {playerHealth.maxHealth}</color>").AppendLine("--------------------").AppendLine("Base Health: 100").AppendLine($"From Strength: +{finalStrength * 10}");
            secondaryStats.healthTooltip = sb.ToString();
        }

        sb.Clear().AppendLine($"<color=blue>Mana: {maxMana}</color>")
            .AppendLine("--------------------")
            .AppendLine("Base Mana: 50")
            .AppendLine($"From Faith: +{finalFaith * 10}")
            .AppendLine($"Regen: {calculatedManaRegen:F1}/sec (From Int)");

        secondaryStats.manaTooltip = sb.ToString();
    }
}

// Simple Helper Attribute for ReadOnly in Inspector
public class ReadOnlyInspectorAttribute : PropertyAttribute { }