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
    [Tooltip("If false, the casting bar UI will not appear for this ability.")]
    public bool showCastBar = true;

    [Tooltip("If true, the player can move freely while casting this ability.")]
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

    // --- AAA UPGRADE: Visual Effects & Feedback ---
    [Header("Visual Feedback (AAA)")]
    [Tooltip("Optional: Override the default targeting reticle.")]
    public GameObject targetingReticleOverride;

    // --- CASTING (WIND-UP) ---
    [Header("Casting (Wind-up) Settings")]
    [Tooltip("VFX played during wind-up (Casting/Telegraph). Loops until execution.")]
    public GameObject castingVFX;
    [Tooltip("Where should the Wind-up VFX appear?")]
    public VFXAnchor castingVFXAnchor = VFXAnchor.LeftHand; // Default to hand for charging
    [Tooltip("Offset (X,Y,Z) relative to the Casting Anchor.")]
    public Vector3 castingVFXPositionOffset;
    [Tooltip("Rotation (X,Y,Z) relative to the Casting Anchor.")]
    public Vector3 castingVFXRotationOffset;
    [Tooltip("Should the wind-up VFX stick to the anchor (move with animation)?")]
    public bool attachCastingVFX = true;

    // --- CAST (EXECUTION) ---
    [Header("Cast (Execution) Settings")]
    [Tooltip("VFX played at the moment of execution (The Swing/Muzzle Flash).")]
    public GameObject castVFX;
    [Tooltip("Where should the Execution VFX appear?")]
    public VFXAnchor castVFXAnchor = VFXAnchor.ProjectileSpawnPoint; // Default to weapon tip
    [Tooltip("Offset (X,Y,Z) relative to the Cast Anchor.")]
    public Vector3 castVFXPositionOffset;
    [Tooltip("Rotation (X,Y,Z) relative to the Cast Anchor.")]
    public Vector3 castVFXRotationOffset;
    [Tooltip("Should the execution VFX stick to the anchor (move with animation)?")]
    public bool attachCastVFX = true;

    [Tooltip("Delay (in seconds) after execution before the CastVFX spawns.")]
    public float castVFXDelay = 0f;

    [Tooltip("VFX played at the target location/impact point.")]
    public GameObject hitVFX;

    [Header("Audio Layering (AAA)")]
    public AudioClip windupSound;
    public AudioClip castSound;
    public AudioClip impactSound;

    [Header("Game Feel (AAA)")]
    [Range(0f, 2f)] public float screenShakeIntensity = 0f;
    [Range(0f, 1f)] public float screenShakeDuration = 0f;

    [Header("Legacy / Simple Effects")]
    public List<string> effects = new List<string>();

    [Header("Effects")]
    [SerializeReference] public List<IAbilityEffect> friendlyEffects = new List<IAbilityEffect>();
    [SerializeReference] public List<IAbilityEffect> hostileEffects = new List<IAbilityEffect>();
}