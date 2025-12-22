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

        // 2. WAIT FOR GAMEMANAGER
        // We wait until the GameManager reports that the scene transition is fully finished.
        // This ensures GameManager has finished setting up the UI (Alpha 1) 
        // BEFORE we tell it to hide the UI (Alpha 0).
        if (GameManager.instance != null)
        {
            while (GameManager.instance.IsTransitioning)
            {
                yield return null;
            }
        }

        // 3. Trigger the integrated sequence
        if (sequenceController != null)
        {
            Debug.Log($"[AutoPlay] Starting Sequence: {uniqueEventID}");
            sequenceController.PlaySequence();

            // 4. Mark as completed immediately so it doesn't loop if we reload/return
            if (playOnlyOnce && UniqueEventSystem.instance != null)
            {
                UniqueEventSystem.instance.MarkEventAsCompleted(uniqueEventID);
            }
        }
        else
        {
            Debug.LogError("[AutoPlay] SequenceController is missing!");
        }
    }
}