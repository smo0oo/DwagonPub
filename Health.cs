using UnityEngine;
using System;

public class Health : MonoBehaviour
{
    public static event Action<DamageInfo> OnDamageTaken;
    public static event Action<HealInfo> OnHealed;
    public event Action OnHealthChanged;

    public event Action OnDowned;
    public event Action OnRevived;

    [Header("Health Stats")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Death Settings")]
    [Tooltip("If true, the object is destroyed at 0 HP (Enemies). If false, it enters Downed state (Players).")]
    public bool destroyOnDeath = true;

    [Tooltip("If true, this character will not take any damage.")]
    public bool isInvulnerable = false;

    // --- State ---
    public bool isDowned { get; private set; } = false;

    [Header("AI Settings")]
    [Tooltip("If this character is an NUC, it will call for help if its health drops below this percentage (0-1).")]
    public float callForHelpThreshold = 0.4f;

    [HideInInspector]
    public float damageReductionPercent = 0f;
    [HideInInspector]
    public Health forwardDamageTo = null;

    private LootGenerator lootGenerator;
    private bool isDead = false;
    private EnemyHealthUI healthUI;
    private PlayerStats playerStats;
    private CharacterRoot root;

    void Awake()
    {
        lootGenerator = GetComponent<LootGenerator>();
        healthUI = GetComponentInChildren<EnemyHealthUI>();
        root = GetComponentInParent<CharacterRoot>();
        if (root != null)
        {
            playerStats = root.PlayerStats;
        }
        currentHealth = maxHealth;
    }

    void Start()
    {
        OnHealthChanged?.Invoke();
    }

    public void TakeDamage(int amount, DamageEffect.DamageType damageType, bool isCrit, GameObject caster)
    {
        if (forwardDamageTo != null)
        {
            forwardDamageTo.TakeDamage(amount, damageType, isCrit, caster);
            return;
        }

        if (isInvulnerable || isDead || isDowned) return;

        int healthBeforeDamage = currentHealth;

        // Mitigation Logic
        float finalDamage = amount;
        if (playerStats != null)
        {
            if (UnityEngine.Random.value < (playerStats.secondaryStats.dodgeChance / 100f)) return; // Dodge
            if (damageType == DamageEffect.DamageType.Magical) finalDamage *= (1 - playerStats.secondaryStats.magicResistance / 100f);
            else finalDamage *= (1 - playerStats.secondaryStats.physicalResistance / 100f);
        }
        finalDamage *= (1f - damageReductionPercent);
        int damageToDeal = Mathf.Max(0, Mathf.FloorToInt(finalDamage));

        currentHealth -= damageToDeal;

        OnDamageTaken?.Invoke(new DamageInfo { Caster = caster, Target = this.gameObject, Amount = damageToDeal, IsCrit = isCrit, DamageType = damageType });

        if ((float)healthBeforeDamage / maxHealth > callForHelpThreshold && (float)currentHealth / maxHealth <= callForHelpThreshold)
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
            root.Animator.SetBool("IsDowned", true);
            root.Animator.SetTrigger("Death");
        }

        ToggleCombatCapability(false);

        // --- NEW: Force Agent Stop ---
        if (root != null)
        {
            var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        OnDowned?.Invoke();
        OnHealthChanged?.Invoke();

        if (PartyManager.instance != null) PartyManager.instance.CheckPartyStatus();
    }

    public void Revive(float healthPercentage = 0.25f)
    {
        isDowned = false;
        isDead = false;

        // Restore HP
        currentHealth = Mathf.FloorToInt(maxHealth * Mathf.Clamp01(healthPercentage));
        if (currentHealth <= 0) currentHealth = 1;

        Debug.Log($"{name} has REVIVED with {currentHealth} HP!");

        if (root != null && root.Animator != null)
        {
            root.Animator.SetBool("IsDowned", false);
            root.Animator.Play("Idle", 0);
        }

        // Re-enable capabilities
        ToggleCombatCapability(true);

        // --- NEW: Force Agent Enable ---
        if (root != null)
        {
            var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = true;
                agent.isStopped = false;
            }
        }

        OnRevived?.Invoke();
        OnHealthChanged?.Invoke();
    }

    private void ToggleCombatCapability(bool canFight)
    {
        if (root != null)
        {
            // Do NOT toggle NavMeshAgent.enabled here repeatedly as it resets pathing; 
            // handled explicitly in BecomeDowned/Revive for safety.
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

    public void UpdateMaxHealth(int newMaxHealth) { maxHealth = newMaxHealth; if (currentHealth > maxHealth) currentHealth = maxHealth; OnHealthChanged?.Invoke(); }
    public void SetToMaxHealth() { currentHealth = maxHealth; OnHealthChanged?.Invoke(); }

    private void Die()
    {
        isDead = true;
        if (lootGenerator != null) lootGenerator.DropLoot();
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (healthUI != null) healthUI.gameObject.SetActive(false);
        Destroy(gameObject, 2f);
    }
}