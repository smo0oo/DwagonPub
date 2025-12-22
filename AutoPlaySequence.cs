using UnityEngine;
using System.Collections;

public class AutoPlaySequence : MonoBehaviour
{
    public SequenceIntegrationController sequenceController;
    public string uniqueEventID;
    public bool playOnlyOnce = true;

    private IEnumerator Start()
    {
        // 1. Check if the system already knows this event is done
        if (playOnlyOnce && UniqueEventSystem.instance != null)
        {
            if (UniqueEventSystem.instance.IsEventCompleted(uniqueEventID))
            {
                yield break; // Exit if already played
            }
        }

        // 2. REMOVED WAIT
        // Removing this prevents PlayerMovement from overriding the spawn position 
        // with stale NavMesh data before the sequence takes control.

        // 3. Trigger the integrated sequence
        if (sequenceController != null)
        {
            Debug.Log($"[AutoPlay] Starting Sequence: {uniqueEventID}");
            sequenceController.PlaySequence();
        }
        else
        {
            Debug.LogError("[AutoPlay] SequenceController is missing!");
        }
    }
}