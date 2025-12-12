using UnityEngine;

[System.Serializable]
public class StatScaling
{
    public StatType stat;
    [Tooltip("The percentage of the stat to add to the effect's power (e.g., 0.5 = 50%).")]
    public float ratio;
}

[System.Serializable]
public class StatModifier
{
    public StatType stat;
    public int value;
}

public enum StatType
{
    Strength,
    Agility,
    Intelligence,
    Faith
}