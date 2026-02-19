using UnityEngine;
using System.Collections.Generic;

public enum AIReactionAction
{
    CastReactionAbility, // The standard response (casts the assigned Ability)
    LeapBackward,        // Physically jump away from the player
    RollAway,            // Ground dash/roll away
    TeleportAway         // Instantly warp to a safer distance
}

// Helper class for phase-based triggers
[System.Serializable]
public class HealthThresholdTrigger
{
    [Tooltip("The ability to use when the boss's health drops below this percentage (0.0 to 1.0).")]
    public Ability abilityToUse;
    [Tooltip("Health percentage (e.g., 0.75 for 75%).")]
    [Range(0f, 1f)]
    public float healthPercentage;
}

// Helper class for reactive triggers
[System.Serializable]
public class ReactiveTrigger
{
    [Header("The Trigger (What causes the reaction)")]
    [Tooltip("If assigned, the enemy will ONLY react to this specific ability. If left blank, it falls back to checking the Trigger Type below.")]
    public Ability specificAbilityTrigger;

    [Tooltip("The broad category of player ability that triggers this (used only if Specific Ability Trigger is empty).")]
    public AbilityType triggerType;

    [Header("The Reaction (What the enemy does)")]
    public AIReactionAction reactionAction = AIReactionAction.CastReactionAbility;

    [Tooltip("The ability to cast (Only used if Reaction Action is set to CastReactionAbility).")]
    public Ability reactionAbility;

    [Tooltip("How far the enemy should move (Only used if Reaction Action is Leap, Roll, or Teleport).")]
    public float movementDistance = 5f;

    [Tooltip("The chance for this reaction to occur (0.0 to 1.0).")]
    [Range(0f, 1f)]
    public float chanceToReact = 0.5f;
}

[CreateAssetMenu(fileName = "New AI Behavior Profile", menuName = "RPG/AI Behavior Profile")]
public class AIBehaviorProfile : ScriptableObject
{
    [Header("Phase Triggers")]
    [Tooltip("A list of abilities to be triggered as the boss's health decreases.")]
    public List<HealthThresholdTrigger> healthTriggers;

    [Header("Reactive Triggers")]
    [Tooltip("A list of reactions to specific types of player abilities.")]
    public List<ReactiveTrigger> reactiveTriggers;
}