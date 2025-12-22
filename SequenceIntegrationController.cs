using UnityEngine;
using UnityEngine.Playables;
using Cinemachine;
using System.Linq;
using UnityEngine.Timeline;

public class SequenceIntegrationController : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector director;

    [Tooltip("The high-priority (100) camera in the CoreScene. Should be DEACTIVATED by default in the Inspector.")]
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
        // 1. Resolve Cross-Scene Reference: Find the Brain sitting in the Core Scene
        BindCinemachineBrain();

        // 2. Activate the Override Camera (The Lens)
        if (sequenceCamera != null)
        {
            // Ensure the GameObject is active so Cinemachine sees it
            sequenceCamera.gameObject.SetActive(true);
            sequenceCamera.Priority = 100;
        }

        // 3. Enter Cinematic State (Input blocking and AI Pause)
        UIInteractionState.IsUIBlockingInput = true;

        if (GameManager.instance != null)
        {
            // NEW: Use the centralized method to hide objects/HUD and stop movement
            GameManager.instance.SetSequenceMode(true);
        }

        // 4. Start the Timeline
        Debug.Log($"[SequenceIntegration] Playing timeline: {director.playableAsset.name}");
        director.Play();
    }

    /// <summary>
    /// Locates the CinemachineBrain (usually in CoreScene) and binds it to the Timeline track.
    /// </summary>
    private void BindCinemachineBrain()
    {
        CinemachineBrain brain = Object.FindAnyObjectByType<CinemachineBrain>();

        if (brain == null)
        {
            Debug.LogError("[SequenceIntegration] FAILED: Could not find CinemachineBrain! Is the Core Scene loaded?");
            return;
        }

        if (director.playableAsset == null) return;

        // Iterate through tracks to find the Cinemachine Track
        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        foreach (var track in timelineAsset.GetOutputTracks())
        {
            if (track is CinemachineTrack)
            {
                // Assign the Brain from the Core Scene to this specific instance of the Timeline
                director.SetGenericBinding(track, brain);
                Debug.Log("[SequenceIntegration] Success: CinemachineBrain bound to Timeline.");
                break;
            }
        }
    }

    private void OnSequenceEnd(PlayableDirector obj)
    {
        // 5. Deactivate the Override Camera so gameplay cameras take back control
        if (sequenceCamera != null)
        {
            sequenceCamera.Priority = 0;
            sequenceCamera.gameObject.SetActive(false);
        }

        // 6. Restore Gameplay State
        UIInteractionState.IsUIBlockingInput = false;

        if (GameManager.instance != null)
        {
            // NEW: Restore the game state via GameManager
            GameManager.instance.SetSequenceMode(false);
        }

        Debug.Log("[SequenceIntegration] Sequence Finished. Control returned to player.");
    }
}