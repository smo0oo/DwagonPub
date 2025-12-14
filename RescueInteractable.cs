using UnityEngine;

public class RescueInteractable : MonoBehaviour, IInteractable
{
    private Health myHealth;

    void Awake()
    {
        myHealth = GetComponent<Health>();
    }

    public void Interact(GameObject interactor)
    {
        if (myHealth != null && myHealth.isDowned)
        {
            // Revive at 25% HP as requested
            myHealth.Revive(0.25f);

            // Play a sound or effect here if desired
            Debug.Log($"{interactor.name} rescued {name}!");

            // Notify manager to check if the mission is complete
            if (DualModeManager.instance != null)
            {
                DualModeManager.instance.CheckRescueProgress();
            }

            // Remove this interaction script so they can't be clicked again
            Destroy(this);
        }
    }
}