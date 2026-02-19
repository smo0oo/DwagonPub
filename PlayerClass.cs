using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Player Class", menuName = "RPG/Player Class")]
public class PlayerClass : ScriptableObject
{
    // --- UPDATED: Expanded AI Roles for Tactical Waypoints ---
    public enum AILogicRole
    {
        MeleeDamage,
        RangedDamage,
        Tank,
        Support,
        Damage // Kept for legacy fallback so existing assets don't break
    }

    [Header("Class Information")]
    public string displayName = "Adventurer";
    [TextArea(4, 10)]
    public string description = "A jack of all trades, master of none.";

    [Tooltip("What role should the AI for this class prioritize? (Affects positioning and tactical node selection)")]
    public AILogicRole aiRole = AILogicRole.MeleeDamage;

    [Header("Base Stats")]
    public int strength = 10;
    public int agility = 10;
    public int intelligence = 10;
    public int faith = 10;

    [Header("Equipment Proficiency")]
    public List<ItemWeaponStats.WeaponCategory> allowedWeaponCategories = new List<ItemWeaponStats.WeaponCategory>();
    public List<ItemArmourStats.ArmourCategory> allowedArmourCategories = new List<ItemArmourStats.ArmourCategory>();

    [Header("Class Skill Tree")]
    [Tooltip("The skill tree that this class will use.")]
    public SkillTree classSkillTree;
}