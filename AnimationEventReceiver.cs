using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationEventReceiver : MonoBehaviour
{
    private PlayerAbilityHolder playerAbilityHolder;
    private EnemyAbilityHolder enemyAbilityHolder;
    private FootstepController footstepController;

    private PlayerEquipment playerEquipment;
    private ProceduralWeaponTrail[] cachedTrails;

    void Awake()
    {
        playerAbilityHolder = GetComponentInParent<PlayerAbilityHolder>();
        enemyAbilityHolder = GetComponentInParent<EnemyAbilityHolder>();
        footstepController = GetComponentInParent<FootstepController>();
        playerEquipment = GetComponentInParent<PlayerEquipment>();
    }

    public void AE_OpenHitbox()
    {
        if (playerAbilityHolder != null) playerAbilityHolder.OnAnimationEventOpenHitbox();
        else if (enemyAbilityHolder != null) enemyAbilityHolder.OnAnimationEventOpenHitbox();
    }

    public void AE_CloseHitbox()
    {
        if (playerAbilityHolder != null) playerAbilityHolder.OnAnimationEventCloseHitbox();
        else if (enemyAbilityHolder != null) enemyAbilityHolder.OnAnimationEventCloseHitbox();
    }

    public void AE_SpawnCastVFX()
    {
        if (playerAbilityHolder != null) playerAbilityHolder.OnAnimationEventSpawnVFX();
        else if (enemyAbilityHolder != null) enemyAbilityHolder.OnAnimationEventSpawnVFX();
    }

    public void AE_PlayImpactAudio()
    {
        if (playerAbilityHolder != null) playerAbilityHolder.OnAnimationEventPlayAudio();
        else if (enemyAbilityHolder != null) enemyAbilityHolder.OnAnimationEventPlayAudio();
    }

    public void AE_FireSingleProjectile()
    {
        if (playerAbilityHolder != null) playerAbilityHolder.OnAnimationEventFireProjectile();
        else if (enemyAbilityHolder != null) enemyAbilityHolder.OnAnimationEventFireProjectile();
    }

    public void AE_Footstep()
    {
        if (footstepController != null) footstepController.PlayFootstep();
    }

    // --- AAA WEAPON TRAIL LINKS ---

    private ProceduralWeaponTrail[] GetTrails()
    {
        // 1. If it's a player, restrict the search ONLY to their active equipment 
        // (so we don't accidentally turn on trails for swords hidden in their backpack)
        if (playerEquipment != null)
        {
            ProceduralWeaponTrail activeTrail = playerEquipment.GetComponentInChildren<ProceduralWeaponTrail>(true);
            if (activeTrail != null) return new ProceduralWeaponTrail[] { activeTrail };
            return null;
        }

        // 2. If it's an enemy/NPC, find any trails attached to their body dynamically.
        // We do this dynamically so it finds weapons that were spawned AFTER Awake().
        ProceduralWeaponTrail[] trails = GetComponentsInChildren<ProceduralWeaponTrail>(true);
        if (trails == null || trails.Length == 0)
        {
            if (transform.parent != null) trails = transform.parent.GetComponentsInChildren<ProceduralWeaponTrail>(true);
        }

        return trails;
    }

    public void AE_StartWeaponTrail()
    {
        cachedTrails = GetTrails();

        if (cachedTrails != null && cachedTrails.Length > 0)
        {
            foreach (var trail in cachedTrails)
            {
                if (trail != null) trail.StartTrail();
            }
        }
        else
        {
            Debug.LogWarning($"[AnimEvent] AE_StartWeaponTrail fired on {gameObject.name}, but no ProceduralWeaponTrail script was found on the weapon!");
        }
    }

    public void AE_StopWeaponTrail()
    {
        // Use the trails we found when the swing started
        if (cachedTrails != null && cachedTrails.Length > 0)
        {
            foreach (var trail in cachedTrails)
            {
                if (trail != null) trail.StopTrail();
            }
        }
        else
        {
            // Fallback just in case
            cachedTrails = GetTrails();
            if (cachedTrails != null)
            {
                foreach (var trail in cachedTrails)
                {
                    if (trail != null) trail.StopTrail();
                }
            }
        }
    }
}