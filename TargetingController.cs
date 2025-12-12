using UnityEngine;
using System;
using System.Collections;

public class TargetingController : MonoBehaviour
{
    public static TargetingController instance;

    [Header("Configuration")]
    public GameObject targetingReticlePrefab;
    public LayerMask groundLayer;
    public Material inRangeMaterial;
    public Material outOfRangeMaterial;

    private GameObject currentReticle;
    private Renderer reticleRenderer;
    private Ability abilityToCast;
    // --- MODIFIED: Changed from PlayerAbilityHolder to the more generic MonoBehaviour ---
    private MonoBehaviour currentCaster;
    private Camera mainCamera;

    public bool IsTargeting { get; private set; } = false;

    public Ability GetCurrentTargetingAbility() => abilityToCast;


    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!IsTargeting) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            currentReticle.transform.position = hit.point;

            if (abilityToCast.range > 0 && currentCaster != null && HotbarManager.instance != null)
            {
                float distance = Vector3.Distance(currentCaster.transform.position, hit.point);
                bool isOutOfRange = distance > abilityToCast.range;

                HotbarManager.instance.SetSlotRangeIndicator(abilityToCast, isOutOfRange);

                if (reticleRenderer != null && inRangeMaterial != null && outOfRangeMaterial != null)
                {
                    reticleRenderer.material = isOutOfRange ? outOfRangeMaterial : inRangeMaterial;
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (currentCaster != null)
            {
                float distance = Vector3.Distance(currentCaster.transform.position, currentReticle.transform.position);
                if (distance <= abilityToCast.range)
                {
                    ConfirmTargeting();
                }
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelTargeting();
        }
    }

    // --- MODIFIED: The 'caster' parameter is now a generic MonoBehaviour ---
    public void StartTargeting(Ability ability, MonoBehaviour caster)
    {
        IsTargeting = true;
        abilityToCast = ability;
        currentCaster = caster;

        currentReticle = Instantiate(targetingReticlePrefab);
        reticleRenderer = currentReticle.GetComponent<Renderer>();

        float diameter = 1f;
        if (ability.abilityType == AbilityType.GroundAOE)
        {
            diameter = ability.aoeRadius * 2;
        }
        else if (ability.abilityType == AbilityType.GroundPlacement || ability.abilityType == AbilityType.Leap || ability.abilityType == AbilityType.Teleport)
        {
            diameter = 1f;
        }

        currentReticle.transform.localScale = new Vector3(diameter, currentReticle.transform.localScale.y, diameter);
    }

    // --- MODIFIED: Now checks the type of the caster before calling the method ---
    private void ConfirmTargeting()
    {
        if (currentCaster != null && abilityToCast != null)
        {
            Vector3 targetPosition = currentReticle.transform.position;

            // Check if the caster is a player or the dome and call the appropriate method
            if (currentCaster is PlayerAbilityHolder playerCaster)
            {
                playerCaster.UseAbility(abilityToCast, targetPosition);
            }
            else if (currentCaster is DomeAbilityHolder domeCaster)
            {
                domeCaster.UseAbility(abilityToCast, targetPosition);
            }
        }
        CancelTargeting();
    }

    public void ConfirmTargetingWithKey()
    {
        if (!IsTargeting) return;

        if (currentCaster != null)
        {
            float distance = Vector3.Distance(currentCaster.transform.position, currentReticle.transform.position);
            if (distance <= abilityToCast.range)
            {
                if (abilityToCast != null)
                {
                    ConfirmTargeting(); // Re-use the new logic here
                }
                else
                {
                    CancelTargeting();
                }
            }
        }
    }

    private void CancelTargeting()
    {
        IsTargeting = false;

        if (abilityToCast != null && HotbarManager.instance != null)
        {
            HotbarManager.instance.SetSlotRangeIndicator(abilityToCast, false);
        }

        if (HotbarManager.instance != null)
        {
            HotbarManager.instance.LockingAbility = null;
        }

        if (currentCaster is PlayerAbilityHolder playerCaster)
        {
            PlayerMovement playerMovement = playerCaster.GetComponentInParent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.IsGroundTargeting = false;
            }
        }

        if (currentReticle != null)
        {
            Destroy(currentReticle);
            currentReticle = null;
        }

        abilityToCast = null;
        currentCaster = null;
    }
}