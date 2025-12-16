using UnityEngine;
using System.Collections.Generic;
using System;

public class CombatLogManager : MonoBehaviour
{
    public static CombatLogManager instance;

    public event Action<string> OnLogEntryAdded;
    private List<string> combatLogEntries = new List<string>();

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
        Health.OnDamageTaken += HandleDamageEvent;
        Health.OnHealed += HandleHealEvent;
        StatusEffectHolder.OnStatusEffectChanged += HandleStatusEffectEvent;
    }

    private void OnDestroy()
    {
        Health.OnDamageTaken -= HandleDamageEvent;
        Health.OnHealed -= HandleHealEvent;
        StatusEffectHolder.OnStatusEffectChanged -= HandleStatusEffectEvent;
    }

    private void AddLogEntry(string entry)
    {
        combatLogEntries.Add(entry);
        OnLogEntryAdded?.Invoke(entry);

        if (combatLogEntries.Count > 100)
        {
            combatLogEntries.RemoveAt(0);
        }
    }

    private string GetCharacterName(GameObject character)
    {
        if (character == null) return "Unknown";
        CharacterRoot root = character.GetComponentInParent<CharacterRoot>();
        return root != null ? root.gameObject.name : character.name;
    }

    public void HandleDamageEvent(DamageInfo info)
    {
        string casterName = GetCharacterName(info.Caster);
        string targetName = GetCharacterName(info.Target);
        string critText = info.IsCrit ? " (Critical!)" : "";
        string log = $"<color=orange>{casterName}</color> hits <color=yellow>{targetName}</color> for <color=red>{info.Amount}</color> {info.DamageType} damage{critText}.";
        AddLogEntry(log);
    }

    public void HandleHealEvent(HealInfo info)
    {
        string casterName = GetCharacterName(info.Caster);
        string targetName = GetCharacterName(info.Target);
        string critText = info.IsCrit ? " (Critical!)" : "";
        string log = $"<color=orange>{casterName}</color> heals <color=yellow>{targetName}</color> for <color=green>{info.Amount}</color> health{critText}.";
        AddLogEntry(log);
    }

    public void HandleStatusEffectEvent(StatusEffectInfo info)
    {
        string targetName = GetCharacterName(info.Target);
        if (info.IsApplied)
        {
            string color = info.Effect.isBuff ? "cyan" : "magenta";
            AddLogEntry($"<color=yellow>{targetName}</color> gains <color={color}>{info.Effect.effectName}</color>.");
        }
        else
        {
            AddLogEntry($"<color=yellow>{targetName}</color>'s <color=grey>{info.Effect.effectName}</color> fades.");
        }
    }
}