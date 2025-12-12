using UnityEngine;
using System.Collections.Generic;

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
    [Tooltip("The type of player ability that will trigger this reaction.")]
    public AbilityType triggerType;
    [Tooltip("The ability the boss will use in response (e.g., a teleport or a shield).")]
    public Ability reactionAbility;
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