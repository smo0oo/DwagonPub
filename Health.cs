using UnityEngine;
using System;

public class Health : MonoBehaviour
{
    // --- Events ---
    public static event Action<DamageInfo> OnDamageTaken;
    public static event Action<HealInfo> OnHealed;
    public event Action OnHealthChanged;

    public event Action OnDowned;
    public event Action OnRevived;
    public event Action OnDeath;

    [Header("Health Stats")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Death Settings")]
    [Tooltip("If true, the object is destroyed at 0 HP (Enemies). If false, it enters Downed state (Players).")]
    public bool destroyOnDeath = true;

    [Header("Invulnerability Settings")]
    public bool isInvulnerable = false;
    public bool debugGodMode = false;

    // --- State ---
    public bool isDowned { get; private set; } = false;

    [Header("AI Settings")]
    [Tooltip("If this character is an NPC, it will call for help if its health drops below this percentage (0-1).")]
    public float callForHelpThreshold = 0.4f;
    private int helpThresholdValue; // Optimization: Calculated once

    [HideInInspector] public float damageReductionPercent = 0f;
    [HideInInspector] public Health forwardDamageTo = null;

    // --- Components ---
    private LootGenerator lootGenerator;
    private bool isDead = false;
    private EnemyHealthUI healthUI;
    private PlayerStats playerStats;
    private CharacterRoot root;

    // --- Optimization Caches ---
    private int originalLayer;
    private static int ignoreRaycastLayer = -1; // Static so we only look it up once per game

    // Animator Hashes (Faster than string lookups)
    private static readonly int IsDownedHash = Animator.StringToHash("IsDowned");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int IdleHash = Animator.StringToHash("Idle");

    void Awake()
    {
        // 1. One-time Layer Lookup
        if (ignoreRaycastLayer == -1) ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        lootGenerator = GetComponent<LootGenerator>();
        healthUI = GetComponentInChildren<EnemyHealthUI>();
        root = GetComponentInParent<CharacterRoot>();

        if (root != null)
        {
            playerStats = root.PlayerStats;
        }

        currentHealth = maxHealth;
        originalLayer = gameObject.layer;

        // 2. Pre-calculate Help Threshold
        UpdateHelpThreshold();
    }

    void Start()
    {
        OnHealthChanged?.Invoke();
    }

    public void UpdateMaxHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        UpdateHelpThreshold();
        OnHealthChanged?.Invoke();
    }

    public void SetToMaxHealth() { currentHealth = maxHealth; OnHealthChanged?.Invoke(); }

    private void UpdateHelpThreshold()
    {
        helpThresholdValue = Mathf.FloorToInt(maxHealth * callForHelpThreshold);
    }

    public void TakeDamage(int amount, DamageEffect.DamageType damageType, bool isCrit, GameObject caster)
    {
        if (forwardDamageTo != null)
        {
            forwardDamageTo.TakeDamage(amount, damageType, isCrit, caster);
            return;
        }

        if (isInvulnerable || debugGodMode || isDead || isDowned) return;

        int healthBeforeDamage = currentHealth;

        // Mitigation Logic
        float finalDamage = amount;
        if (playerStats != null)
        {
            // Dodge Calculation
            if (UnityEngine.Random.value < (playerStats.secondaryStats.dodgeChance / 100f)) return;

            // Resistance Calculation
            if (damageType == DamageEffect.DamageType.Magical)
                finalDamage *= (1 - playerStats.secondaryStats.magicResistance / 100f);
            else
                finalDamage *= (1 - playerStats.secondaryStats.physicalResistance / 100f);
        }

        finalDamage *= (1f - damageReductionPercent);
        int damageToDeal = Mathf.Max(0, Mathf.FloorToInt(finalDamage));

        currentHealth -= damageToDeal;

        // Global Event
        OnDamageTaken?.Invoke(new DamageInfo { Caster = caster, Target = this.gameObject, Amount = damageToDeal, IsCrit = isCrit, DamageType = damageType });

        // Optimization: Integer comparison is faster than float division
        if (healthBeforeDamage > helpThresholdValue && currentHealth <= helpThresholdValue)
        {
            PartyMemberAI ai = GetComponentInParent<PartyMemberAI>();
            if (ai != null && ai.enabled) { PartyAIManager.instance.CallForHelp(this.gameObject); }
        }

        if (FloatingTextManager.instance != null)
        {
            FloatingTextManager.instance.ShowDamage(damageToDeal, isCrit, damageType, transform.position + Vector3.up * 4.0f);
        }

        if (currentHealth < 0) currentHealth = 0;
        OnHealthChanged?.Invoke();

        if (currentHealth <= 0)
        {
            if (destroyOnDeath) Die();
            else BecomeDowned();
        }
    }

    private void BecomeDowned()
    {
        if (isDowned) return;
        isDowned = true;
        currentHealth = 0;

        Debug.Log($"{name} is DOWNED!");

        if (root != null && root.Animator != null)
        {
            root.Animator.SetBool(IsDownedHash, true);
            root.Animator.SetTrigger(DeathHash);
        }

        ToggleCombatCapability(false);

        if (root != null)
        {
            var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        gameObject.layer = ignoreRaycastLayer;

        OnDowned?.Invoke();
        OnHealthChanged?.Invoke();

        if (PartyManager.instance != null) PartyManager.instance.CheckPartyStatus();
    }

    public void Revive(float healthPercentage = 0.25f)
    {
        isDowned = false;
        isDead = false;

        currentHealth = Mathf.FloorToInt(maxHealth * Mathf.Clamp01(healthPercentage));
        if (currentHealth <= 0) currentHealth = 1;

        Debug.Log($"{name} has REVIVED with {currentHealth} HP!");

        if (root != null && root.Animator != null)
        {
            root.Animator.SetBool(IsDownedHash, false);
            root.Animator.Play(IdleHash, 0);
        }

        ToggleCombatCapability(true);

        if (root != null)
        {
            var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                agent.isStopped = false;
            }
        }

        gameObject.layer = originalLayer;

        OnRevived?.Invoke();
        OnHealthChanged?.Invoke();
    }

    public void ForceDownedState()
    {
        isDowned = true;
        currentHealth = 0;

        ToggleCombatCapability(false);

        if (root != null)
        {
            var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        gameObject.layer = ignoreRaycastLayer;

        if (root != null && root.Animator != null)
        {
            root.Animator.SetBool(IsDownedHash, true);
        }

        Debug.Log($"{name} forcibly set to DOWNED state.");
    }

    private void ToggleCombatCapability(bool canFight)
    {
        if (root != null)
        {
            if (root.PlayerMovement != null) root.PlayerMovement.enabled = canFight;
            if (root.PartyMemberAI != null) root.PartyMemberAI.enabled = canFight;
        }
    }

    public void Heal(int amount, GameObject caster, bool isCrit = false)
    {
        if (isDead || isDowned) return;
        int healthBeforeHeal = currentHealth;
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        int actualHealedAmount = currentHealth - healthBeforeHeal;

        OnHealed?.Invoke(new HealInfo { Caster = caster, Target = this.gameObject, Amount = actualHealedAmount, IsCrit = isCrit });

        if (FloatingTextManager.instance != null && actualHealedAmount > 0)
        {
            FloatingTextManager.instance.ShowHeal(actualHealedAmount, isCrit, transform.position + Vector3.up * 4.0f);
        }
        OnHealthChanged?.Invoke();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1. Notify Systems (EnemyAI, Quests, etc)
        OnDeath?.Invoke();

        // 2. Play Animation
        if (root != null && root.Animator != null)
        {
            root.Animator.SetTrigger(DeathHash);
        }
        else
        {
            // Fallback for simple enemies
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(DeathHash);
        }

        // 3. Drop Loot
        if (lootGenerator != null) lootGenerator.DropLoot();

        // 4. Disable Physical Presence
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (healthUI != null) healthUI.gameObject.SetActive(false);

        // 5. Cleanup
        // Note: EnemyAI has its own cleanup, but this ensures non-AI objects still disappear.
        Destroy(gameObject, 3f);
    }
}