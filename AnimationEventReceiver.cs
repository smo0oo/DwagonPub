using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationEventReceiver : MonoBehaviour
{
    private PlayerAbilityHolder abilityHolder;

    void Awake()
    {
        // Find the holder on the root object
        abilityHolder = GetComponentInParent<PlayerAbilityHolder>();
    }

    // These exact names will be typed into the Animation Event in Unity
    public void AE_OpenHitbox()
    {
        if (abilityHolder != null) abilityHolder.OnAnimationEventOpenHitbox();
    }

    public void AE_CloseHitbox()
    {
        if (abilityHolder != null) abilityHolder.OnAnimationEventCloseHitbox();
    }

    public void AE_SpawnCastVFX()
    {
        if (abilityHolder != null) abilityHolder.OnAnimationEventSpawnVFX();
    }

    public void AE_PlayImpactAudio()
    {
        if (abilityHolder != null) abilityHolder.OnAnimationEventPlayAudio();
    }
}