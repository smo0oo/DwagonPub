// PartyAIEnums.cs

/// <summary>
/// Defines the high-level commands a player can issue to NUCs.
/// </summary>
public enum AICommand
{
    Follow,
    AttackTarget,
    MoveToAndDefend,
    // --- NEW: Add this command ---
    HealTarget
}

/// <summary>
/// Defines the general behavior or "stance" of an NUC.
/// </summary>
public enum AIStance
{
    Aggressive,
    Defensive,
    Passive
}