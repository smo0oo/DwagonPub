using UnityEngine;
using UnityEngine.Playables;
using Cinemachine;
using System.Linq;

public class SequenceIntegrationController : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector director;

    [Tooltip("The high-priority (100) camera in the CoreScene. Should be DEACTIVATED by default.")]
    public CinemachineVirtualCamera sequenceCamera;

    private void OnEnable()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        director.stopped += OnSequenceEnd;
    }

    private void OnDisable()
    {
        if (director != null) director.stopped -= OnSequenceEnd;
    }

    [ContextMenu("Play Sequence")]
    public void PlaySequence()
    {
        // 1. Resolve Cross-Scene Reference
        BindCinemachineBrain();

        // 2. Activate the Override Camera
        if (sequenceCamera != null)
        {
            sequenceCamera.gameObject.SetActive(true); // Turn it on so it can hijack the view
            sequenceCamera.Priority = 100;
        }

        // 3. Enter Cinematic State
        UIInteractionState.IsUIBlockingInput = true;

        if (GameManager.instance != null)
        {
            GameManager.instance.SetPlayerMovementComponentsActive(false);
            if (GameManager.instance.sharedCanvasGroup != null)
                GameManager.instance.sharedCanvasGroup.alpha = 0f;
        }

        director.Play();
    }

    private void BindCinemachineBrain()
    {
        CinemachineBrain brain = Object.FindAnyObjectByType<CinemachineBrain>();

        if (brain == null)
        {
            Debug.LogError("[SequenceIntegration] Could not find CinemachineBrain!");
            return;
        }

        var timelineAsset = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
        var cinemachineTrack = timelineAsset.GetOutputTracks().OfType<CinemachineTrack>().FirstOrDefault();

        if (cinemachineTrack != null)
        {
            director.SetGenericBinding(cinemachineTrack, brain);
        }
    }

    private void OnSequenceEnd(PlayableDirector obj)
    {
        // 4. Deactivate the Override Camera
        if (sequenceCamera != null)
        {
            sequenceCamera.Priority = 0;
            sequenceCamera.gameObject.SetActive(false); // Turn it off so gameplay cameras return
        }

        // 5. Restore Gameplay State
        UIInteractionState.IsUIBlockingInput = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.SetPlayerMovementComponentsActive(true);
            if (GameManager.instance.sharedCanvasGroup != null)
                GameManager.instance.sharedCanvasGroup.alpha = 1f;
        }
    }
}