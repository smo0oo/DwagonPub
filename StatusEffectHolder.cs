using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class StatusEffectHolder : MonoBehaviour
{
    public static event Action<StatusEffectInfo> OnStatusEffectChanged;
    public event Action<StatusEffectHolder> OnEffectsChanged;

    private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();
    private PlayerStats playerStats;
    private Health health;
    private Animator animator;

    public bool IsStunned { get; private set; }
    public bool IsRooted { get; private set; }

    private void Awake()
    {
        playerStats = GetComponentInParent<PlayerStats>();
        health = GetComponentInParent<Health>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect effect = activeEffects[i];
            effect.Update(Time.deltaTime);
            if (effect.IsFinished)
            {
                RemoveStatusEffect(effect);
            }
        }
    }

    public void AddStatusEffect(StatusEffect effectData, GameObject caster)
    {
        if (effectData.durationType == DurationType.Instant)
        {
            foreach (var effect in effectData.tickEffects) { effect.Apply(caster, this.gameObject); }
            return;
        }

        var newEffect = new ActiveStatusEffect(effectData, caster, this);

        // --- AAA FIX: Spawn Persistent VFX & Trigger Animation ---
        if (effectData.persistentVFX != null)
        {
            Transform anchor = GetAnchorTransform(effectData.vfxAnchor);
            GameObject vfxInstance = ObjectPooler.instance.Get(effectData.persistentVFX, anchor.position, anchor.rotation);
            if (vfxInstance != null)
            {
                vfxInstance.transform.SetParent(anchor);
                vfxInstance.SetActive(true);
                newEffect.VfxInstance = vfxInstance;
            }
        }

        if (!string.IsNullOrEmpty(effectData.applyAnimationTrigger) && animator != null)
        {
            animator.SetTrigger(effectData.applyAnimationTrigger);
        }
        // ---------------------------------------------------------

        activeEffects.Add(newEffect);
        ApplyStatModifiers(newEffect, true);
        UpdateEntityStates();

        OnStatusEffectChanged?.Invoke(new StatusEffectInfo { Target = this.gameObject, Effect = effectData, IsApplied = true });
        OnEffectsChanged?.Invoke(this);
    }

    private void RemoveStatusEffect(ActiveStatusEffect effectToRemove)
    {
        // --- AAA FIX: Cleanup Persistent VFX ---
        if (effectToRemove.VfxInstance != null)
        {
            if (effectToRemove.VfxInstance.TryGetComponent<PooledObject>(out var pooled)) pooled.ReturnToPool();
            else Destroy(effectToRemove.VfxInstance);
        }

        if (effectToRemove.EffectData.isStun && animator != null)
        {
            animator.SetTrigger("ForceIdle"); // Break out of stun animation loop
        }
        // ---------------------------------------

        ApplyStatModifiers(effectToRemove, false);
        activeEffects.Remove(effectToRemove);

        UpdateEntityStates();

        OnStatusEffectChanged?.Invoke(new StatusEffectInfo { Target = this.gameObject, Effect = effectToRemove.EffectData, IsApplied = false });
        OnEffectsChanged?.Invoke(this);
    }

    public void ClearAllNegativeEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (!activeEffects[i].EffectData.isBuff) RemoveStatusEffect(activeEffects[i]);
        }
    }

    private void UpdateEntityStates()
    {
        if (health != null) health.isInvulnerable = activeEffects.Any(e => e.EffectData.grantsInvulnerability);
        IsStunned = activeEffects.Any(e => e.EffectData.isStun);
        IsRooted = activeEffects.Any(e => e.EffectData.isRoot);
    }

    public List<ActiveStatusEffect> GetActiveEffects() { return activeEffects; }

    private void ApplyStatModifiers(ActiveStatusEffect activeEffect, bool apply)
    {
        if (playerStats == null || activeEffect.EffectData.statModifiers.Count == 0) return;
        int multiplier = apply ? 1 : -1;
        foreach (var modifier in activeEffect.EffectData.statModifiers)
        {
            switch (modifier.stat)
            {
                case StatType.Strength: playerStats.bonusStrength += modifier.value * multiplier; break;
                case StatType.Agility: playerStats.bonusAgility += modifier.value * multiplier; break;
                case StatType.Intelligence: playerStats.bonusIntelligence += modifier.value * multiplier; break;
                case StatType.Faith: playerStats.bonusFaith += modifier.value * multiplier; break;
            }
        }
        playerStats.CalculateFinalStats();
    }

    private Transform GetAnchorTransform(VFXAnchor anchor)
    {
        if (animator != null && animator.isHuman)
        {
            switch (anchor)
            {
                case VFXAnchor.Head: return animator.GetBoneTransform(HumanBodyBones.Head) ?? transform;
                case VFXAnchor.Center: return animator.GetBoneTransform(HumanBodyBones.Chest) ?? transform;
                case VFXAnchor.LeftHand: return animator.GetBoneTransform(HumanBodyBones.LeftHand) ?? transform;
                case VFXAnchor.RightHand: return animator.GetBoneTransform(HumanBodyBones.RightHand) ?? transform;
                case VFXAnchor.Feet: return transform;
            }
        }
        return transform;
    }
}

public class ActiveStatusEffect
{
    public StatusEffect EffectData { get; }
    public GameObject Caster { get; }
    public GameObject VfxInstance { get; set; } // <--- Added to track the VFX
    public bool IsFinished { get; private set; }
    public float RemainingDuration { get; private set; }
    private float tickTimer;
    private StatusEffectHolder target;

    public ActiveStatusEffect(StatusEffect effectData, GameObject caster, StatusEffectHolder targetHolder)
    {
        EffectData = effectData;
        Caster = caster;
        target = targetHolder;
        IsFinished = false;
        RemainingDuration = EffectData.duration;
        tickTimer = EffectData.tickRate;
    }

    public void Update(float deltaTime)
    {
        if (EffectData.durationType == DurationType.Timed)
        {
            RemainingDuration -= deltaTime;
            if (RemainingDuration <= 0) { IsFinished = true; return; }
        }
        if (EffectData.tickRate > 0)
        {
            tickTimer -= deltaTime;
            if (tickTimer <= 0) { tickTimer = EffectData.tickRate; ApplyTickEffects(); }
        }
    }

    private void ApplyTickEffects() { foreach (var effect in EffectData.tickEffects) { effect.Apply(Caster, target.gameObject); } }
}