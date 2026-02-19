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

public enum VFXAnchor
{
    ProjectileSpawnPoint, // Default (Weapon tip)
    LeftHand,
    RightHand,
    Feet,                 // Root
    Center,               // Chest/Torso
    Head
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
    public string abilityName = "New Ability";
    public string displayName = "New Ability Rank 1";
    public int rank = 1;
    public Sprite icon;
    [TextArea(3, 5)] public string description = "Ability Description";
    public float cooldown = 1.0f;
    public int manaCost = 10;

    [Header("AI Settings")]
    public int priority = 1;
    public bool isAreaEffect = false;
    public AIUsageType usageType = AIUsageType.StandardDamage;

    [Header("Casting Configuration")]
    public bool showCastBar = true;
    public bool canMoveWhileCasting = false;
    public float castTime = 0f;
    public bool triggersGlobalCooldown = true;
    public float telegraphDuration = 0f;
    public string telegraphAnimationTrigger;

    [Header("Animation Settings")]
    public int attackStyleIndex = 0;
    public string overrideTriggerName;
    public float movementLockDuration = 0f;

    [Header("Behavior Type")]
    public AbilityType abilityType = AbilityType.TargetedMelee;
    public bool locksPlayerActivity = false;

    [Header("Weapon Requirement")]
    public bool requiresWeaponType = false;
    public List<ItemWeaponStats.WeaponCategory> requiredWeaponCategories = new List<ItemWeaponStats.WeaponCategory>();

    [Header("Gameplay Properties")]
    public float range = 2f;
    public GameObject playerProjectilePrefab;
    public GameObject enemyProjectilePrefab;
    public bool canHitCaster = false;

    [Header("Melee Hitbox")]
    public Vector3 attackBoxSize = new Vector3(1, 2, 2);
    public Vector3 attackBoxCenter = new Vector3(0, 1, 1);
    public float hitboxOpenDelay = 0.4f;
    public float hitboxCloseDelay = 0.5f;

    [Header("Area of Effect")]
    public float aoeRadius = 5f;

    [Header("Placement")]
    public GameObject placementPrefab;

    [Header("Channeled Beam")]
    public float manaDrain = 15f;
    public float tickRate = 0.25f;

    [Header("Visual Feedback (AAA)")]
    public GameObject targetingReticleOverride;

    [Header("Casting (Wind-up) Settings")]
    public GameObject castingVFX;
    public VFXAnchor castingVFXAnchor = VFXAnchor.LeftHand;
    public Vector3 castingVFXPositionOffset;
    public Vector3 castingVFXRotationOffset;
    public bool attachCastingVFX = true;

    [Header("Cast (Execution) Settings")]
    public GameObject castVFX;
    public VFXAnchor castVFXAnchor = VFXAnchor.ProjectileSpawnPoint;
    public Vector3 castVFXPositionOffset;
    public Vector3 castVFXRotationOffset;
    public bool attachCastVFX = true;
    public float castVFXDelay = 0f;

    // --- AAA UPGRADE: Impact Visuals ---
    [Header("Hit / Impact VFX Settings")]
    [Tooltip("VFX played at the target location or impact point.")]
    public GameObject hitVFX;

    [Tooltip("Use this to fix VFX that spawn too high or too low. Positive Y moves it up.")]
    public Vector3 hitVFXPositionOffset;

    [Tooltip("Allows you to tilt the impact VFX (useful for ground decals or directional bursts).")]
    public Vector3 hitVFXRotationOffset;
    // -----------------------------------

    [Header("Audio Layering (AAA)")]
    public AudioClip windupSound;
    public AudioClip castSound;
    public AudioClip impactSound;

    [Header("Game Feel (AAA)")]
    [Range(0f, 2f)] public float screenShakeIntensity = 0f;
    [Range(0f, 1f)] public float screenShakeDuration = 0f;

    // --- NEW: Enemy AI Telegraphing ---
    [Header("Enemy AI Settings")]
    [Tooltip("The specific warning decal to spawn for this ability (e.g. Green Splat for Poison). If null, uses the default Red Circle.")]
    public GameObject enemyTelegraphPrefab;
    // ----------------------------------

    [Header("Legacy / Simple Effects")]
    public List<string> effects = new List<string>();

    [Header("Effects")]
    [SerializeReference] public List<IAbilityEffect> friendlyEffects = new List<IAbilityEffect>();
    [SerializeReference] public List<IAbilityEffect> hostileEffects = new List<IAbilityEffect>();
}