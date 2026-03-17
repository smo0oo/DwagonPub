using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationEventReceiver : MonoBehaviour
{
    private PlayerAbilityHolder playerAbilityHolder;
    private EnemyAbilityHolder enemyAbilityHolder;

    void Awake()
    {
        playerAbilityHolder = GetComponentInParent<PlayerAbilityHolder>();
        enemyAbilityHolder = GetComponentInParent<EnemyAbilityHolder>();
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
}