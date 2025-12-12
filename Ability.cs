using UnityEngine;
using System.Collections.Generic;

public enum AbilityType
{
    TargetedProjectile,
    TargetedMelee,
    GroundAOE,
    Self,
    ForwardProjectile,
    GroundPlacement,
    ChanneledBeam,
    Leap,
    Charge,
    Teleport,
    DirectionalMelee
}

public enum AIUsageType
{
    StandardDamage,
    Finisher,
    AoeDamage,
    SingleTargetHeal,
    AoeHeal,
    WagonPassiveAura,
    WagonAutoTurret,
    WagonCallable,
    DefensiveBuff,
    AllyBuff,
    Control
}

[CreateAssetMenu(fileName = "New Ability", menuName = "RPG/Ability")]
public class Ability : ScriptableObject
{
    [Header("Core Information")]
    [Tooltip("The internal, unique name for this ability (e.g., 'fireball_rank_1').")]
    public string abilityName = "New Ability";
    [Tooltip("The display name shown to the player in UIs and tooltips.")]
    public string displayName = "New Ability Rank 1";
    [Tooltip("The rank of this ability, used for skill trees and learning.")]
    public int rank = 1;
    [Tooltip("The icon shown on hotbars and in the ability book.")]
    public Sprite icon;
    [Tooltip("The description shown in tooltips.")]
    [TextArea(3, 5)]
    public string description = "Ability Description";
    [Tooltip("The time in seconds before this ability can be used again.")]
    public float cooldown = 1.0f;
    [Tooltip("The amount of mana this ability costs to use.")]
    public int manaCost = 10;

    [Header("AI Settings")]
    [Tooltip("The AI's priority for using this ability. Higher numbers are used first.")]
    public int priority = 1;
    [Tooltip("Is this an Area of Effect ability? (Used for AI decision-making).")]
    public bool isAreaEffect = false;
    [Tooltip("What tactical role does this ability fill for the AI?")]
    public AIUsageType usageType = AIUsageType.StandardDamage;

    [Header("Casting")]
    [Tooltip("The time in seconds the caster must stand still to cast this ability.")]
    public float castTime = 0f;
    [Tooltip("Does this ability trigger the shared global cooldown?")]
    public bool triggersGlobalCooldown = true;
    [Tooltip("How long (in seconds) a warning should appear before the cast begins.")]
    public float telegraphDuration = 0f;
    [Tooltip("The name of the trigger in the Animator to play for the telegraph wind-up.")]
    public string telegraphAnimationTrigger;

    [Header("Animation Settings")]
    [Tooltip("0 = Standard, 1 = Heavy/Overhead, 2 = Thrust/Stab. Used by the Animator to select a specific attack variation within the weapon state.")]
    public int attackStyleIndex = 0;

    [Tooltip("If set, this string will trigger a specific parameter in the Animator (e.g., 'Kick'), bypassing the standard weapon attack logic.")]
    public string overrideTriggerName;

    // --- FIX: This is the missing field causing your error ---
    [Tooltip("How many seconds the player is rooted in place when using this ability. Set to 0 for no lock.")]
    public float movementLockDuration = 0f;
    // --------------------------------------------------------

    [Header("Behavior Type")]
    [Tooltip("The core logic this ability follows (e.g., fires a projectile, hits in melee, places on ground).")]
    public AbilityType abilityType = AbilityType.TargetedMelee;
    [Tooltip("If true, the player cannot move or use other abilities while this ability is active (e.g., ChanneledBeam).")]
    public bool locksPlayerActivity = false;

    [Header("Weapon Requirement")]
    [Tooltip("If true, the caster must have a specific weapon type equipped to use this ability.")]
    public bool requiresWeaponType = false;
    [Tooltip("The list of weapon categories that are allowed to use this ability.")]
    public List<ItemWeaponStats.WeaponCategory> requiredWeaponCategories = new List<ItemWeaponStats.WeaponCategory>();


    [Header("Gameplay Properties")]
    [Tooltip("The maximum distance (in meters) from which this ability can be used.")]
    public float range = 2f;
    [Tooltip("The projectile prefab to spawn when a player or party member uses this ability.")]
    public GameObject playerProjectilePrefab;
    [Tooltip("The projectile prefab to spawn when an enemy uses this ability.")]
    public GameObject enemyProjectilePrefab;
    [Tooltip("Can this ability's effects (e.g., projectiles, melee swings) hit the caster?")]
    public bool canHitCaster = false;

    [Header("Melee Hitbox (for TargetedMelee / DirectionalMelee)")]
    [Tooltip("The size (X, Y, Z) of the hitbox, relative to the caster.")]
    public Vector3 attackBoxSize = new Vector3(1, 2, 2);
    [Tooltip("The center point of the hitbox, relative to the caster.")]
    public Vector3 attackBoxCenter = new Vector3(0, 1, 1);
    [Tooltip("The delay (in seconds) after execution before the hitbox becomes active.")]
    public float hitboxOpenDelay = 0.4f;
    [Tooltip("The delay (in seconds) after execution when the hitbox deactivates.")]
    public float hitboxCloseDelay = 0.5f;

    [Header("Area of Effect (for GroundAOE / Self)")]
    [Tooltip("The radius (in meters) for ground-targeted or self-cast AOE abilities.")]
    public float aoeRadius = 5f;

    [Header("Placement (for GroundPlacement)")]
    [Tooltip("The prefab (e.g., a trap) to instantiate at the target location.")]
    public GameObject placementPrefab;

    [Header("Channeled Beam")]
    [Tooltip("The mana cost per tick (defined by Tick Rate).")]
    public float manaDrain = 15f;
    [Tooltip("How often (in seconds) the beam applies its effects and drains mana.")]
    public float tickRate = 0.25f;

    [Header("Feedback")]
    [Tooltip("The visual effect prefab to spawn on the caster when the ability is executed.")]
    public GameObject castVFX;
    [Tooltip("The visual effect prefab to spawn at the impact point (for projectiles, AOEs, etc.).")]
    public GameObject hitVFX;
    [Tooltip("The sound effect to play when the ability is executed.")]
    public AudioClip castSound;

    [Header("Effects Applied to Allies")]
    [SerializeReference]
    [Tooltip("The list of effects (e.g., Heal, ApplyStatusEffect) to apply to any friendly targets.")]
    public List<IAbilityEffect> friendlyEffects = new List<IAbilityEffect>();

    [Header("Effects Applied to Enemies")]
    [SerializeReference]
    [Tooltip("The list of effects (e.g., Damage, ApplyStatusEffect) to apply to any hostile targets.")]
    public List<IAbilityEffect> hostileEffects = new List<IAbilityEffect>();
}