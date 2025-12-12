using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Class", menuName = "RPG/Enemy Class")]
public class EnemyClass : ScriptableObject
{
    [Header("Core Stats")]
    public int level = 1;
    public int maxHealth = 50;

    [Header("Combat Modifiers")]
    [Tooltip("Multiplier for all outgoing damage. 1.0 = 100% (normal), 1.5 = 150%, etc.")]
    public float damageMultiplier = 1.0f;

    [Tooltip("Percentage of incoming damage to ignore. 0.0 = 0% mitigation, 0.25 = 25% mitigation.")]
    [Range(0f, 0.9f)]
    public float damageMitigation = 0f;
}