using UnityEngine;
using System.Collections.Generic;

public enum DurationType
{
    Instant,
    Timed,
    Infinite
}

[CreateAssetMenu(fileName = "New Status Effect", menuName = "RPG/Status Effect")]
public class StatusEffect : ScriptableObject
{
    [Header("Core Information")]
    public string effectName;
    public Sprite icon;
    [Tooltip("Is this a helpful effect (buff) or a harmful one (debuff)? This can be used for UI color and dispel logic.")]
    public bool isBuff;

    // --- NEW: Invulnerability Flag ---
    [Header("Special Properties")]
    [Tooltip("If true, the target will take 0 damage from all sources while this effect is active.")]
    public bool grantsInvulnerability = false;
    // --------------------------------

    [Header("Duration")]
    public DurationType durationType;
    [Tooltip("Duration in seconds (only if Timed).")]
    public float duration;

    [Header("Stat Modifiers")]
    [Tooltip("Static changes to primary stats that are applied for the duration of the effect.")]
    public List<StatModifier> statModifiers;

    [Header("Effects Over Time (DoT / HoT)")]
    [Tooltip("How often the tick effects should apply (in seconds). Set to 0 if they should not tick.")]
    public float tickRate;

    [SerializeReference] // This is crucial for the effects list to work correctly
    [Tooltip("Effects that apply on each tick (e.g., a DamageEffect for poison, or a HealEffect for regeneration).")]
    public List<IAbilityEffect> tickEffects;
}