using UnityEngine;
using System.Collections.Generic;

public enum AIReactionAction
{
    CastReactionAbility,
    LeapBackward,
    RollAway,
    TeleportAway
}

[System.Serializable]
public class HealthThresholdTrigger
{
    [Tooltip("Health percentage (e.g., 0.75 for 75%).")]
    [Range(0f, 1f)]
    public float healthPercentage;

    [Tooltip("The ability to use when the boss's health drops below this percentage.")]
    public Ability abilityToUse;

    [Header("Phase Transition Mechanics")]
    [Tooltip("If true, the boss will become invulnerable, play an animation, and clear debuffs.")]
    public bool isPhaseTransition = false;
    [Tooltip("How long the boss is invulnerable during the transition.")]
    public float invulnerabilityDuration = 3f;
    [Tooltip("The Animator Trigger to play when entering this phase (e.g. 'Roar').")]
    public string phaseAnimationTrigger = "Roar";
    [Tooltip("Should poison/bleeds/debuffs be cleared on phase change?")]
    public bool clearDebuffsOnPhase = true;
}

[System.Serializable]
public class ReactiveTrigger
{
    [Header("The Trigger (What causes the reaction)")]
    public Ability specificAbilityTrigger;
    public AbilityType triggerType;

    [Header("The Reaction (What the enemy does)")]
    public AIReactionAction reactionAction = AIReactionAction.CastReactionAbility;
    public Ability reactionAbility;
    public float movementDistance = 5f;
    [Range(0f, 1f)] public float chanceToReact = 0.5f;
}

[CreateAssetMenu(fileName = "New AI Behavior Profile", menuName = "RPG/AI Behavior Profile")]
public class AIBehaviorProfile : ScriptableObject
{
    [Header("Phase Triggers")]
    public List<HealthThresholdTrigger> healthTriggers;

    [Header("Reactive Triggers")]
    public List<ReactiveTrigger> reactiveTriggers;
}