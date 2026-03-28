using UnityEngine;
using UnityEngine.VFX;
using System;
using System.Collections;
using UnityEngine.AI;

public class Health : MonoBehaviour
{
    // --- Global Events ---
    public static event Action<DamageInfo> OnDamageTaken;
    public static event Action<HealInfo> OnHealed;

    // --- Local Events ---
    public event Action OnHealthChanged;
    public event Action OnDowned;
    public event Action OnRevived;
    public event Action OnDeath;

    public event Action<int> OnTakeLocalDamage;

    [Header("Health Stats")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Poise & Knockback (AAA)")]
    public float maxPoise = 100f;
    public float currentPoise;
    public float poiseRegenRate = 15f;

    [Header("Death Settings")]
    public bool destroyOnDeath = true;

    [Header("Invulnerability Settings")]
    public bool isInvulnerable = false;
    public bool debugGodMode = false;

    [Header("Visual Effects (Surface System)")]
    public SurfaceDefinition surfaceDefinition;
    public Vector3 hitVFXOffset = new Vector3(0, 1.5f, 0);

    public bool isDowned { get; private set; } = false;

    [Header("AI Settings")]
    public float callForHelpThreshold = 0.4f;
    private int helpThresholdValue;

    [HideInInspector] public float damageReductionPercent = 0f;
    [HideInInspector] public Health forwardDamageTo = null;

    // --- Cached Components ---
    private LootGenerator lootGenerator;
    private EnemyHealthUI healthUI;
    private PlayerStats playerStats;
    private CharacterRoot root;
    private NavMeshAgent cachedAgent;
    private EnemyAbilityHolder cachedEnemyCaster;
    private Animator cachedFallbackAnimator;

    private bool isDead = false;
    private int originalLayer;
    private static int ignoreRaycastLayer = -1;

    private static readonly int IsDownedHash = Animator.StringToHash("IsDowned");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int FlinchHash = Animator.StringToHash("Flinch");

    void Awake()
    {
        if (ignoreRaycastLayer == -1) ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        lootGenerator = GetComponent<LootGenerator>();
        healthUI = GetComponentInChildren<EnemyHealthUI>();
        root = GetComponentInParent<CharacterRoot>();

        if (root != null)
        {
            playerStats = root.PlayerStats;
            cachedAgent = root.GetComponent<NavMeshAgent>();
        }
        else
        {
            cachedAgent = GetComponent<NavMeshAgent>();
        }

        cachedEnemyCaster = GetComponent<EnemyAbilityHolder>() ?? GetComponentInChildren<EnemyAbilityHolder>();
        cachedFallbackAnimator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
        currentPoise = maxPoise;
        originalLayer = gameObject.layer;

        UpdateHelpThreshold();
    }

    void Start()
    {
        OnHealthChanged?.Invoke();
    }

    void Update()
    {
        if (currentPoise < maxPoise && !isDead && !isDowned)
        {
            currentPoise += poiseRegenRate * Time.deltaTime;
            if (currentPoise > maxPoise) currentPoise = maxPoise;
        }
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

    public void TakeDamage(int amount, DamageEffect.DamageType damageType, bool isCrit, GameObject caster, float knockback = 0f, int poiseDamage = 0)
    {
        // 1. Forward damage (e.g., Dome edge markers forward damage to the main Dome)
        if (forwardDamageTo != null)
        {
            forwardDamageTo.TakeDamage(amount, damageType, isCrit, caster, knockback, poiseDamage);
            return;
        }

        if (isInvulnerable || debugGodMode || isDead || isDowned) return;

        if (GameManager.instance != null)
        {
            GameManager.instance.TriggerHitStop(0.05f, 0.1f);
        }

        int healthBeforeDamage = currentHealth;
        float finalDamage = amount;

        // 2. Player-specific mitigations
        if (playerStats != null)
        {
            if (UnityEngine.Random.value < (playerStats.secondaryStats.dodgeChance / 100f))
            {
                if (FloatingTextManager.instance != null)
                    FloatingTextManager.instance.ShowText("Dodge!", transform.position + Vector3.up * 2f, Color.yellow);
                return;
            }

            if (damageType == DamageEffect.DamageType.Magical)
                finalDamage *= (1 - playerStats.secondaryStats.magicResistance / 100f);
            else
                finalDamage *= (1 - playerStats.secondaryStats.physicalResistance / 100f);
        }

        // 3. Global Mitigation (This perfectly handles the Dome's damageReductionPercent!)
        finalDamage *= (1f - damageReductionPercent);

        int damageToDeal = Mathf.Max(0, Mathf.FloorToInt(finalDamage));
        currentHealth -= damageToDeal;

        if (damageToDeal > 0)
        {
            OnTakeLocalDamage?.Invoke(damageToDeal);
        }

        // 4. Poise & Knockback
        currentPoise -= poiseDamage;
        if (currentHealth > 0 && currentPoise <= 0)
        {
            currentPoise = maxPoise;

            if (root != null && root.Animator != null) root.Animator.SetTrigger(FlinchHash);
            else if (cachedFallbackAnimator != null) cachedFallbackAnimator.SetTrigger(FlinchHash);

            if (root != null && root.PlayerAbilityHolder != null) root.PlayerAbilityHolder.CancelCast();
            if (cachedEnemyCaster != null) cachedEnemyCaster.CancelCast();

            if (knockback > 0f && caster != null)
            {
                Vector3 pushDir = (transform.position - caster.transform.position).normalized;
                pushDir.y = 0;
                StartCoroutine(ApplyKnockback(pushDir, knockback));
            }
        }

        // 5. Visuals & Audio
        if (surfaceDefinition != null && damageToDeal > 0)
        {
            surfaceDefinition.GetReaction(damageType, out GameObject prefab, out VisualEffectAsset graph, out AudioClip sound);

            Quaternion rotation = Quaternion.identity;
            if (caster != null)
            {
                Vector3 dir = (caster.transform.position - transform.position).normalized;
                if (dir != Vector3.zero) rotation = Quaternion.LookRotation(dir);
            }

            GameObject vfxInstance = null;

            if (graph != null && surfaceDefinition.vfxContainerPrefab != null)
            {
                vfxInstance = ObjectPooler.instance.Get(surfaceDefinition.vfxContainerPrefab, transform.position + hitVFXOffset, rotation);

                if (vfxInstance != null)
                {
                    if (vfxInstance.TryGetComponent<VisualEffect>(out var vfxComp))
                    {
                        vfxComp.visualEffectAsset = graph;
                        vfxComp.Play();
                    }
                    vfxInstance.SetActive(true);
                }
            }
            else if (prefab != null)
            {
                vfxInstance = ObjectPooler.instance.Get(prefab, transform.position + hitVFXOffset, rotation);
                if (vfxInstance != null) vfxInstance.SetActive(true);
            }

            if (sound != null)
            {
                AudioSource.PlayClipAtPoint(sound, transform.position);
            }
        }

        OnDamageTaken?.Invoke(new DamageInfo { Caster = caster, Target = this.gameObject, Amount = damageToDeal, IsCrit = isCrit, DamageType = damageType });

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

    private IEnumerator ApplyKnockback(Vector3 direction, float force, float duration = 0.2f)
    {
        float elapsed = 0f;

        if (cachedAgent != null && cachedAgent.isOnNavMesh)
        {
            bool wasStopped = cachedAgent.isStopped;
            cachedAgent.isStopped = true;

            while (elapsed < duration)
            {
                if (isDead || isDowned) break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float strength = Mathf.Lerp(force, 0, t);
                cachedAgent.Move(direction * strength * Time.deltaTime * 10f);
                yield return null;
            }

            if (cachedAgent.isOnNavMesh && !isDead && !isDowned) cachedAgent.isStopped = wasStopped;
        }
    }

    private void BecomeDowned()
    {
        if (isDowned) return;
        isDowned = true;
        currentHealth = 0;

        ToggleCombatCapability(false);

        if (cachedAgent != null && cachedAgent.isActiveAndEnabled && cachedAgent.isOnNavMesh)
        {
            cachedAgent.isStopped = true;
            cachedAgent.ResetPath();
            cachedAgent.velocity = Vector3.zero;
        }

        gameObject.layer = ignoreRaycastLayer;

        if (root != null && root.Animator != null)
        {
            root.Animator.SetFloat("VelocityX", 0f);
            root.Animator.SetFloat("VelocityZ", 0f);
            root.Animator.SetTrigger(DeathHash);
            StartCoroutine(ApplyDownedBoolDelayed(root.Animator));
        }

        OnDowned?.Invoke();
        OnHealthChanged?.Invoke();

        if (PartyManager.instance != null) PartyManager.instance.CheckPartyStatus();
    }

    private IEnumerator ApplyDownedBoolDelayed(Animator anim)
    {
        yield return new WaitForSeconds(1.5f);
        if (anim != null) anim.SetBool(IsDownedHash, true);
    }

    public void Revive(float healthPercentage = 0.25f)
    {
        isDowned = false;
        isDead = false;

        currentHealth = Mathf.FloorToInt(maxHealth * Mathf.Clamp01(healthPercentage));
        if (currentHealth <= 0) currentHealth = 1;
        currentPoise = maxPoise;

        if (root != null && root.Animator != null)
        {
            root.Animator.SetBool(IsDownedHash, false);
            root.Animator.Play(IdleHash, 0);
        }

        ToggleCombatCapability(true);

        if (cachedAgent != null)
        {
            cachedAgent.enabled = true;
            cachedAgent.isStopped = false;
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

        if (cachedAgent != null && cachedAgent.isActiveAndEnabled && cachedAgent.isOnNavMesh)
        {
            cachedAgent.isStopped = true;
            cachedAgent.ResetPath();
            cachedAgent.velocity = Vector3.zero;
        }

        gameObject.layer = ignoreRaycastLayer;

        if (root != null && root.Animator != null)
        {
            root.Animator.SetFloat("VelocityX", 0f);
            root.Animator.SetFloat("VelocityZ", 0f);
            root.Animator.SetBool(IsDownedHash, true);
            root.Animator.Play("Death", 0, 1.0f);
        }
    }

    private void ToggleCombatCapability(bool canFight)
    {
        if (root != null)
        {
            if (root.PlayerMovement != null) root.PlayerMovement.enabled = canFight;
            if (root.PartyMemberAI != null) root.PartyMemberAI.enabled = canFight;

            if (PartyManager.instance != null && PartyManager.instance.partyMembers.Contains(root.gameObject))
            {
                bool isCurrentlyActive = (PartyManager.instance.ActivePlayer == root.gameObject);

                if (root.PlayerMovement != null) root.PlayerMovement.enabled = (canFight && isCurrentlyActive);
                if (root.PartyMemberAI != null) root.PartyMemberAI.enabled = (canFight && !isCurrentlyActive);
            }
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

        OnDeath?.Invoke();
        ToggleCombatCapability(false);

        if (cachedAgent != null && cachedAgent.isActiveAndEnabled && cachedAgent.isOnNavMesh)
        {
            cachedAgent.isStopped = true;
            cachedAgent.ResetPath();
            cachedAgent.velocity = Vector3.zero;
        }

        if (root != null && root.Animator != null)
        {
            root.Animator.SetFloat("VelocityX", 0f);
            root.Animator.SetFloat("VelocityZ", 0f);
            root.Animator.SetTrigger(DeathHash);
        }
        else if (cachedFallbackAnimator != null)
        {
            cachedFallbackAnimator.SetFloat("VelocityX", 0f);
            cachedFallbackAnimator.SetFloat("VelocityZ", 0f);
            cachedFallbackAnimator.SetTrigger(DeathHash);
        }

        if (lootGenerator != null) lootGenerator.DropLoot();

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (healthUI != null) healthUI.gameObject.SetActive(false);

        Destroy(gameObject, 3f);
    }
}