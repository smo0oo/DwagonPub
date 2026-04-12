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
    DirectionalMelee,
    Grenade
}

public enum VFXAnchor
{
    ProjectileSpawnPoint,
    LeftHand,
    RightHand,
    Feet,
    Center,
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

public enum ScreenShakeEpicenter
{
    Caster,
    TargetOrLocation,
    GlobalCamera
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

    [Header("Randomization")]
    public bool randomizeAttackStyle = false;
    public int maxRandomVariants = 3;

    [Header("Manual Combo System (AAA)")]
    public Ability nextComboLink;
    public float comboWindow = 1.5f;
    public bool bypassGcdOnCombo = true;

    [Header("Behavior Type")]
    public AbilityType abilityType = AbilityType.TargetedMelee;
    public bool locksPlayerActivity = false;

    [Header("Weapon Requirement")]
    public bool requiresWeaponType = false;
    public List<ItemWeaponStats.WeaponCategory> requiredWeaponCategories = new List<ItemWeaponStats.WeaponCategory>();

    [Header("Gameplay Properties")]
    public float range = 2f;
    public bool canHitCaster = false;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float projectileSpawnDelay = 0f;
    public bool piercesEnemies = false;

    [Header("Multi-Projectile Settings (AAA)")]
    public bool useCoroutineForProjectiles = true;
    public int projectileCount = 1;
    public float burstDelay = 0f;
    public float spreadAngle = 0f;

    [Header("Melee Hitbox")]
    public Vector3 attackBoxSize = new Vector3(1, 2, 2);
    public Vector3 attackBoxCenter = new Vector3(0, 1, 1);
    public float hitboxOpenDelay = 0.4f;
    public float hitboxCloseDelay = 0.5f;

    [Header("Dynamic Hitbox (AAA)")]
    [Tooltip("If true, the hitbox will smoothly interpolate from the starting size/center to the end size/center while active.")]
    public bool useDynamicHitbox = false;
    public Vector3 endAttackBoxSize = new Vector3(2, 2, 4);
    public Vector3 endAttackBoxCenter = new Vector3(0, 1, 2);
    [Tooltip("Controls the easing of the hitbox expansion over its active duration. 0 is start size, 1 is end size.")]
    public AnimationCurve hitboxScaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Area of Effect")]
    public float aoeRadius = 5f;

    [Header("Placement")]
    public GameObject placementPrefab;

    [Header("Channeled Beam")]
    public GameObject channeledBeamPrefab;
    public VFXAnchor channeledBeamAnchor = VFXAnchor.ProjectileSpawnPoint;
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

    [Header("Weapon Trail Override (AAA)")]
    [Tooltip("If true, the procedural weapon trail will be recolored during this attack.")]
    public bool overrideWeaponTrailColor = false;
    [ColorUsage(true, true)] public Color weaponTrailColor = Color.white;

    [Header("Attack Style VFX Overrides")]
    public List<StyleVFXOverride> styleVFXOverrides = new List<StyleVFXOverride>();

    [Header("Hit / Impact VFX Settings")]
    public GameObject hitVFX;
    public Vector3 hitVFXPositionOffset;
    public Vector3 hitVFXRotationOffset;

    [Header("Audio Layering (AAA)")]
    public AudioClip windupSound;
    public AudioClip castSound;
    public AudioClip impactSound;
    public VoiceEffort voiceEffort = VoiceEffort.None;

    [Header("Game Feel (AAA)")]
    [Range(0f, 2f)] public float screenShakeIntensity = 0f;
    [Range(0f, 5f)] public float screenShakeDuration = 0f;
    public float screenShakeDelay = 0f;
    public ScreenShakeEpicenter screenShakeEpicenter = ScreenShakeEpicenter.Caster;

    [Header("Enemy AI Settings")]
    public GameObject enemyTelegraphPrefab;
    public bool isMajorTacticalThreat = false;

    [Header("Effects")]
    [SerializeReference] public List<IAbilityEffect> onCastEffects = new List<IAbilityEffect>();
    [SerializeReference] public List<IAbilityEffect> friendlyEffects = new List<IAbilityEffect>();
    [SerializeReference] public List<IAbilityEffect> hostileEffects = new List<IAbilityEffect>();
}

[System.Serializable]
public struct StyleVFXOverride
{
    public GameObject overrideVFX;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    [Header("Custom VFX Graph Data")]
    [Tooltip("If true, injects the Vector2 data below into the instantiated VFX Prefab.")]
    public bool passCustomData;
    [Tooltip("The exact string name of the exposed Vector2 property in your VFX Graph (e.g., 'CustomData', 'Scale').")]
    public string propertyName;
    public Vector2 customData;

    [Header("Weapon Trail")]
    [Tooltip("If true, overrides the base ability weapon trail color just for this combo swing.")]
    public bool overrideTrailColor;
    [ColorUsage(true, true)] public Color trailColor;
}