using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

public class DomeAI : MonoBehaviour
{
    [Header("AI Settings")]
    public float detectionRadius = 40f;
    public LayerMask enemyLayer;

    [Header("Abilities")]
    [Tooltip("Abilities the Dome should know by default.")]
    public List<Ability> defaultAbilities = new List<Ability>();

    private DomeAbilityHolder abilityHolder;
    private List<Ability> passiveAbilities;
    private List<Ability> autoTurretAbilities;

    private Collider[] _enemyBuffer = new Collider[50];

    // --- OPTIMIZATION VARIABLES ---
    private float aiTickTimer = 0f;
    private const float AI_TICK_RATE = 0.25f; // Run AI logic 4 times per second
    // ------------------------------

    void Awake()
    {
        abilityHolder = GetComponent<DomeAbilityHolder>();
    }

    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        CategorizeAbilities();
    }

    void Update()
    {
        if (abilityHolder == null) return;
        if (passiveAbilities == null || autoTurretAbilities == null) return;

        // --- OPTIMIZATION: Throttle AI Logic ---
        aiTickTimer += Time.deltaTime;
        if (aiTickTimer >= AI_TICK_RATE)
        {
            HandlePassiveAbilities();
            HandleAutoTurretAbilities();
            aiTickTimer = 0f;
        }
        // ---------------------------------------
    }

    private GameObject FindNearestEnemy(float maxRange)
    {
        if (abilityHolder == null) return null;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _enemyBuffer, enemyLayer);

        if (hitCount == 0) return null;

        GameObject closest = null;
        float minDistance = float.MaxValue;

        Vector3 sightOrigin = (abilityHolder.projectileSpawnPoint != null)
            ? abilityHolder.projectileSpawnPoint.position
            : transform.position + Vector3.up;

        int domeLayer = LayerMask.NameToLayer("Dome");
        int layerMask = ~(1 << domeLayer);

        for (int i = 0; i < hitCount; i++)
        {
            var enemy = _enemyBuffer[i];
            if (enemy == null) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < maxRange && distance < minDistance)
            {
                Vector3 targetPosition = enemy.transform.position + Vector3.up;

                if (!Physics.Linecast(sightOrigin, targetPosition, out RaycastHit hit, layerMask))
                {
                    minDistance = distance;
                    closest = enemy.gameObject;
                }
                else
                {
                    if (hit.transform.IsChildOf(enemy.transform) || hit.transform == enemy.transform)
                    {
                        minDistance = distance;
                        closest = enemy.gameObject;
                    }
                }
            }
        }

        return closest;
    }

    private void HandleAutoTurretAbilities()
    {
        foreach (var ability in autoTurretAbilities)
        {
            GameObject nearestEnemy = FindNearestEnemy(ability.range);
            if (nearestEnemy != null && abilityHolder.CanUseAbility(ability, nearestEnemy))
            {
                abilityHolder.UseAbility(ability, nearestEnemy);
            }
        }
    }

    private void CategorizeAbilities()
    {
        if (defaultAbilities == null) return;
        passiveAbilities = defaultAbilities.Where(a => a.usageType == AIUsageType.WagonPassiveAura).ToList();
        autoTurretAbilities = defaultAbilities.Where(a => a.usageType == AIUsageType.WagonAutoTurret).ToList();
    }

    private void HandlePassiveAbilities()
    {
        foreach (var ability in passiveAbilities)
        {
            if (abilityHolder.CanUseAbility(ability, this.gameObject))
            {
                abilityHolder.UseAbility(ability, this.gameObject);
            }
        }
    }
}