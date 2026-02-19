using UnityEngine;

public enum AICommand
{
    Follow,
    AttackTarget,
    HealTarget,
    MoveToAndDefend,
    Evade // NEW: Running to a tactical node
}

public enum AIStance
{
    Defensive,  // Attack what the player attacks / defend self
    Aggressive, // Attack enemies in sight freely
    Passive     // Never attack, just follow (good for sneaking or retreating)
}