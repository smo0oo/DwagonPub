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
    private MonoBehaviour currentCaster;
    private Camera mainCamera;

    // --- NEW: Keep track of which player we locked so we can unlock them later ---
    private PlayerMovement lockedPlayerMovement;

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

    public void StartTargeting(Ability ability, MonoBehaviour caster)
    {
        if (IsTargeting)
        {
            CancelTargeting();
        }

        IsTargeting = true;
        abilityToCast = ability;
        currentCaster = caster;

        // --- FIX START: Lock the Active Player regardless of who is casting ---
        // Previously, this only worked if 'caster' was the player. 
        // Now we explicitly find the Active Player to stop them moving while the Wagon (or anyone else) aims.

        PlayerMovement pmToLock = null;

        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            pmToLock = PartyManager.instance.ActivePlayer.GetComponent<PlayerMovement>();
        }
        else if (caster is PlayerAbilityHolder playerCaster)
        {
            // Fallback for single-player testing scenes
            pmToLock = playerCaster.GetComponentInParent<PlayerMovement>();
        }

        if (pmToLock != null)
        {
            lockedPlayerMovement = pmToLock;
            lockedPlayerMovement.IsGroundTargeting = true;
        }
        // --- FIX END ---

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

    private void ConfirmTargeting()
    {
        try
        {
            if (currentCaster != null && abilityToCast != null)
            {
                Vector3 targetPosition = currentReticle.transform.position;

                if (currentCaster is PlayerAbilityHolder playerCaster)
                {
                    playerCaster.UseAbility(abilityToCast, targetPosition);
                }
                else if (currentCaster is DomeAbilityHolder domeCaster)
                {
                    domeCaster.UseAbility(abilityToCast, targetPosition);
                }
            }
        }
        finally
        {
            CancelTargeting();
        }
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
                    ConfirmTargeting();
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

        // --- FIX: Safely unlock the specific player we locked earlier ---
        if (lockedPlayerMovement != null)
        {
            lockedPlayerMovement.StartCoroutine(lockedPlayerMovement.ResetGroundTargetingFlag());
            lockedPlayerMovement = null;
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