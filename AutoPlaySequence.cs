using UnityEngine;
using UnityEngine.Playables;

public class AutoPlaySequence : MonoBehaviour
{
    public SequenceIntegrationController sequenceController;
    public string uniqueEventID;
    public bool playOnlyOnce = true;

    private void Start()
    {
        // Check if we should play based on the event system
        if (playOnlyOnce && UniqueEventSystem.instance != null)
        {
            if (UniqueEventSystem.instance.IsEventCompleted(uniqueEventID))
            {
                // Already played, don't trigger again
                return;
            }
        }

        // Trigger the integrated sequence (handles pausing/camera override)
        if (sequenceController != null)
        {
            sequenceController.PlaySequence();
        }
    }
}