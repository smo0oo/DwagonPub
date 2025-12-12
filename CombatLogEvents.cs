// CombatLogEvents.cs

using UnityEngine;

// A container for all information related to a damage event.
public struct DamageInfo
{
    public GameObject Caster;
    public GameObject Target;
    public int Amount;
    public bool IsCrit;
    public DamageEffect.DamageType DamageType;
}

// A container for all information related to a healing event.
public struct HealInfo
{
    public GameObject Caster;
    public GameObject Target;
    public int Amount;
    public bool IsCrit;
}

// A container for all information related to an ability cast.
public struct CastInfo
{
    public GameObject Caster;
    public GameObject Target;
    public Ability Ability;
}

// A container for all information related to a status effect change.
public struct StatusEffectInfo
{
    public GameObject Target;
    public StatusEffect Effect;
    public bool IsApplied; // True if applied, false if it faded
}