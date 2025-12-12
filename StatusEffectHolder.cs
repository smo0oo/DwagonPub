using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class StatusEffectHolder : MonoBehaviour
{
    // --- NEW: Event for status effect changes ---
    public static event Action<StatusEffectInfo> OnStatusEffectChanged;

    public event Action<StatusEffectHolder> OnEffectsChanged;

    private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();
    private PlayerStats playerStats;

    private void Awake() { playerStats = GetComponentInParent<PlayerStats>(); }
    private void Update() { for (int i = activeEffects.Count - 1; i >= 0; i--) { ActiveStatusEffect effect = activeEffects[i]; effect.Update(Time.deltaTime); if (effect.IsFinished) { RemoveStatusEffect(effect); } } }

    public void AddStatusEffect(StatusEffect effectData, GameObject caster)
    {
        if (effectData.durationType == DurationType.Instant)
        {
            foreach (var effect in effectData.tickEffects) { effect.Apply(caster, this.gameObject); }
            return;
        }

        var newEffect = new ActiveStatusEffect(effectData, caster, this);
        activeEffects.Add(newEffect);
        ApplyStatModifiers(newEffect, true);

        // --- NEW: Announce that the effect was applied ---
        OnStatusEffectChanged?.Invoke(new StatusEffectInfo { Target = this.gameObject, Effect = effectData, IsApplied = true });

        OnEffectsChanged?.Invoke(this);
    }

    private void RemoveStatusEffect(ActiveStatusEffect effectToRemove)
    {
        ApplyStatModifiers(effectToRemove, false);
        activeEffects.Remove(effectToRemove);

        // --- NEW: Announce that the effect has faded ---
        OnStatusEffectChanged?.Invoke(new StatusEffectInfo { Target = this.gameObject, Effect = effectToRemove.EffectData, IsApplied = false });

        OnEffectsChanged?.Invoke(this);
    }

    public List<ActiveStatusEffect> GetActiveEffects() { return activeEffects; }
    private void ApplyStatModifiers(ActiveStatusEffect activeEffect, bool apply) { if (playerStats == null || activeEffect.EffectData.statModifiers.Count == 0) return; int multiplier = apply ? 1 : -1; foreach (var modifier in activeEffect.EffectData.statModifiers) { switch (modifier.stat) { case StatType.Strength: playerStats.bonusStrength += modifier.value * multiplier; break; case StatType.Agility: playerStats.bonusAgility += modifier.value * multiplier; break; case StatType.Intelligence: playerStats.bonusIntelligence += modifier.value * multiplier; break; case StatType.Faith: playerStats.bonusFaith += modifier.value * multiplier; break; } } playerStats.CalculateFinalStats(); }
}

public class ActiveStatusEffect { public StatusEffect EffectData { get; } public GameObject Caster { get; } public bool IsFinished { get; private set; } public float RemainingDuration { get; private set; } private float tickTimer; private StatusEffectHolder target; public ActiveStatusEffect(StatusEffect effectData, GameObject caster, StatusEffectHolder targetHolder) { EffectData = effectData; Caster = caster; target = targetHolder; IsFinished = false; RemainingDuration = EffectData.duration; tickTimer = EffectData.tickRate; } public void Update(float deltaTime) { if (EffectData.durationType == DurationType.Timed) { RemainingDuration -= deltaTime; if (RemainingDuration <= 0) { IsFinished = true; return; } } if (EffectData.tickRate > 0) { tickTimer -= deltaTime; if (tickTimer <= 0) { tickTimer = EffectData.tickRate; ApplyTickEffects(); } } } private void ApplyTickEffects() { foreach (var effect in EffectData.tickEffects) { effect.Apply(Caster, target.gameObject); } } }