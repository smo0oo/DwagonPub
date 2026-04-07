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
        // 1. Try to find the master CharacterRoot script to search the entire prefab
        CharacterRoot charRoot = GetComponentInParent<CharacterRoot>();

        if (charRoot != null)
        {
            playerAbilityHolder = charRoot.GetComponentInChildren<PlayerAbilityHolder>(true);
            enemyAbilityHolder = charRoot.GetComponentInChildren<EnemyAbilityHolder>(true);
            footstepController = charRoot.GetComponentInChildren<FootstepController>(true);
            playerEquipment = charRoot.GetComponentInChildren<PlayerEquipment>(true);
        }
        else
        {
            // 2. Fallback: Search up, then search the absolute root of this object tree
            Transform entityRoot = transform.root;

            playerAbilityHolder = GetComponentInParent<PlayerAbilityHolder>() ?? entityRoot.GetComponentInChildren<PlayerAbilityHolder>(true);
            enemyAbilityHolder = GetComponentInParent<EnemyAbilityHolder>() ?? entityRoot.GetComponentInChildren<EnemyAbilityHolder>(true);
            footstepController = GetComponentInParent<FootstepController>() ?? entityRoot.GetComponentInChildren<FootstepController>(true);
            playerEquipment = GetComponentInParent<PlayerEquipment>() ?? entityRoot.GetComponentInChildren<PlayerEquipment>(true);
        }

        if (playerAbilityHolder == null && enemyAbilityHolder == null)
        {
            Debug.LogError($"<color=red>[AnimEvent]</color> Receiver on {gameObject.name} could NOT find an AbilityHolder anywhere on the character!");
        }
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
        if (playerAbilityHolder != null)
        {
            playerAbilityHolder.OnAnimationEventSpawnVFX();
        }
        else if (enemyAbilityHolder != null)
        {
            enemyAbilityHolder.OnAnimationEventSpawnVFX();
        }
        else
        {
            Debug.LogError($"<color=red>[AnimEvent]</color> AE_SpawnCastVFX fired, but there is no Player or Enemy Ability Holder attached!");
        }
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
        Transform searchRoot = transform;

        CharacterRoot charRoot = GetComponentInParent<CharacterRoot>();
        if (charRoot != null)
        {
            searchRoot = charRoot.transform;
        }
        else
        {
            searchRoot = transform.root;
        }

        // AAA FIX: Search the absolute root of the character. 
        // Passing 'false' ensures we ONLY grab trails on currently active meshes (weapons in hands),
        // completely ignoring any inactive weapons hidden in the backpack!
        ProceduralWeaponTrail[] trails = searchRoot.GetComponentsInChildren<ProceduralWeaponTrail>(false);

        // Absolute fallback just in case the weapon trail component itself was manually disabled
        if (trails == null || trails.Length == 0)
        {
            trails = searchRoot.GetComponentsInChildren<ProceduralWeaponTrail>(true);
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
            Debug.LogWarning($"<color=yellow>[AnimEvent]</color> AE_StartWeaponTrail fired on {gameObject.name}, but no ProceduralWeaponTrail script was found!");
        }
    }

    public void AE_StopWeaponTrail()
    {
        if (cachedTrails != null && cachedTrails.Length > 0)
        {
            foreach (var trail in cachedTrails)
            {
                if (trail != null) trail.StopTrail();
            }
        }
        else
        {
            // Fallback lookup
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