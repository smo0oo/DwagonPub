using UnityEngine;
using System;

public class Health : MonoBehaviour
{
    public static event Action<DamageInfo> OnDamageTaken;
    public static event Action<HealInfo> OnHealed;
    public event Action OnHealthChanged;

    [Header("Health Stats")]
    public int maxHealth = 100;
    public int currentHealth;

    [Tooltip("If true, this character will not take any damage.")]
    public bool isInvulnerable = false;

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

    void Awake()
    {
        lootGenerator = GetComponent<LootGenerator>();
        healthUI = GetComponentInChildren<EnemyHealthUI>();
        CharacterRoot root = GetComponentInParent<CharacterRoot>();
        if (root != null)
        {
            playerStats = root.PlayerStats;
        }

        // --- FIX: Initialize currentHealth in Awake() ---
        // This guarantees currentHealth has a value before any Start() method runs.
        currentHealth = maxHealth;
    }

    void Start()
    {
        // --- MODIFIED: The initialization is removed from Start() ---
        // We still invoke the event here to make sure all UIs
        // (like the main player portrait) get an initial value.
        OnHealthChanged?.Invoke();
    }

    public void TakeDamage(int amount, DamageEffect.DamageType damageType, bool isCrit, GameObject caster)
    {
        if (forwardDamageTo != null)
        {
            forwardDamageTo.TakeDamage(amount, damageType, isCrit, caster);
            return;
        }

        if (isInvulnerable || isDead) return;

        int healthBeforeDamage = currentHealth;

        if (playerStats != null) { if (UnityEngine.Random.value < (playerStats.secondaryStats.dodgeChance / 100f)) { FloatingTextManager.instance.ShowEvent("Dodge", transform.position + Vector3.up * 4.0f); return; } if (damageType == DamageEffect.DamageType.Physical) { if (UnityEngine.Random.value < (playerStats.secondaryStats.parryChance / 100f)) { FloatingTextManager.instance.ShowEvent("Parry", transform.position + Vector3.up * 4.0f); return; } } if (UnityEngine.Random.value < (playerStats.secondaryStats.blockChance / 100f)) { amount = Mathf.FloorToInt(amount * 0.5f); FloatingTextManager.instance.ShowEvent("Block", transform.position + Vector3.up * 4.0f); } }
        float finalDamage = amount;
        if (playerStats != null) { if (damageType == DamageEffect.DamageType.Magical) { finalDamage *= (1 - playerStats.secondaryStats.magicResistance / 100f); } else { finalDamage *= (1 - playerStats.secondaryStats.physicalResistance / 100f); } }

        finalDamage *= (1f - damageReductionPercent);

        int damageToDeal = Mathf.Max(0, Mathf.FloorToInt(finalDamage));
        bool wasOverkill = false;
        int overkillAmount = 0;
        if (damageToDeal >= currentHealth) { wasOverkill = true; overkillAmount = damageToDeal - currentHealth; }
        currentHealth -= damageToDeal;

        OnDamageTaken?.Invoke(new DamageInfo { Caster = caster, Target = this.gameObject, Amount = damageToDeal, IsCrit = isCrit, DamageType = damageType });

        if ((float)healthBeforeDamage / maxHealth > callForHelpThreshold && (float)currentHealth / maxHealth <= callForHelpThreshold) { PartyMemberAI ai = GetComponentInParent<PartyMemberAI>(); if (ai != null && ai.enabled) { PartyAIManager.instance.CallForHelp(this.gameObject); Debug.Log($"<color=yellow>{name} is calling for help!</color>"); } }
        if (FloatingTextManager.instance != null) { Vector3 textPosition = transform.position + Vector3.up * 4.0f; if (wasOverkill) { FloatingTextManager.instance.ShowOverkill(overkillAmount, textPosition); } else { FloatingTextManager.instance.ShowDamage(damageToDeal, isCrit, damageType, textPosition); } }
        if (currentHealth < 0) currentHealth = 0;
        OnHealthChanged?.Invoke();
        if (currentHealth <= 0) { Die(); }
    }

    public void UpdateMaxHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        OnHealthChanged?.Invoke();
    }

    public void SetToMaxHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke();
    }

    public void Heal(int amount, GameObject caster, bool isCrit = false)
    {
        if (isDead) return;

        int healthBeforeHeal = currentHealth;
        currentHealth += amount;

        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        int actualHealedAmount = currentHealth - healthBeforeHeal;

        OnHealed?.Invoke(new HealInfo { Caster = caster, Target = this.gameObject, Amount = actualHealedAmount, IsCrit = isCrit });

        if (FloatingTextManager.instance != null && actualHealedAmount > 0)
        {
            Vector3 textPosition = transform.position + Vector3.up * 4.0f;
            FloatingTextManager.instance.ShowHeal(actualHealedAmount, isCrit, textPosition);
        }

        OnHealthChanged?.Invoke();
    }

    private void Die()
    {
        isDead = true;
        if (lootGenerator != null)
        {
            lootGenerator.DropLoot();
        }
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
        if (healthUI != null)
        {
            healthUI.gameObject.SetActive(false);
        }
        Destroy(gameObject, 2f);
    }
}