using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterIKRelay : MonoBehaviour
{
    [Tooltip("Drag the parent object with the PlayerMovement script here.")]
    public PlayerMovement playerMovement;

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
        // Attempt to find parent if not assigned
        if (playerMovement == null) playerMovement = GetComponentInParent<PlayerMovement>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (playerMovement == null || animator == null) return;

        // 1. Get the data calculated by the PlayerMovement script
        float weight = playerMovement.CurrentHeadLookWeight;
        Vector3 target = playerMovement.CurrentHeadLookPosition;

        // 2. Apply it to this Animator
        if (weight > 0)
        {
            animator.SetLookAtWeight(weight, 0.2f, 0.5f, 0.7f, 0.5f);
            animator.SetLookAtPosition(target);
        }
    }
}