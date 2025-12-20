using UnityEngine;

public class SequenceTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The controller that pauses gameplay and plays the Timeline.")]
    public SequenceIntegrationController sequenceController;

    [Header("Trigger Settings")]
    [Tooltip("A unique string ID for this event (e.g., 'Intro_Cutscene_01').")]
    public string uniqueEventID;

    [Tooltip("If true, this will only trigger if the ID hasn't been completed yet.")]
    public bool triggerOnlyOnce = true;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Check if the system already knows this event is done
        if (triggerOnlyOnce && UniqueEventSystem.instance != null)
        {
            if (UniqueEventSystem.instance.IsEventCompleted(uniqueEventID))
            {
                return;
            }
        }

        // 2. Check if the object entering is a player
        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            if (sequenceController != null)
            {
                // 3. Mark as completed in the persistent system
                if (triggerOnlyOnce && UniqueEventSystem.instance != null)
                {
                    UniqueEventSystem.instance.MarkEventAsCompleted(uniqueEventID);
                }

                // 4. Play the sequence
                sequenceController.PlaySequence();
            }
        }
    }
}