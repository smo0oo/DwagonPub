using UnityEngine;
using System.Collections; // Required for Coroutines

[RequireComponent(typeof(LootGenerator))]
[RequireComponent(typeof(Animator))]
public class LootChest : MonoBehaviour, IInteractable
{
    [Tooltip("The name of the trigger parameter in the Animator to play the open animation.")]
    public string openAnimationTrigger = "Open";

    [Tooltip("A brief delay (in seconds) to wait after the animation starts before spawning loot.")]
    public float lootSpawnDelay = 0.2f;

    private LootGenerator lootGenerator;
    private Animator animator;
    private bool hasBeenOpened = false;

    void Awake()
    {
        lootGenerator = GetComponent<LootGenerator>();
        animator = GetComponent<Animator>();
    }

    public void Interact(GameObject interactor)
    {
        if (hasBeenOpened)
        {
            return;
        }

        hasBeenOpened = true;
        StartCoroutine(OpenChestRoutine());
    }

    private IEnumerator OpenChestRoutine()
    {
        // First, trigger the animation.
        if (animator != null && !string.IsNullOrEmpty(openAnimationTrigger))
        {
            animator.SetTrigger(openAnimationTrigger);
        }

        // Wait for the specified delay.
        yield return new WaitForSeconds(lootSpawnDelay);

        // Then, spawn the loot.
        if (lootGenerator != null)
        {
            lootGenerator.DropLoot();
        }
    }
}