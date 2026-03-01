using UnityEngine;
using PixelCrushers.DialogueSystem;

[RequireComponent(typeof(AudioSource))]
public class QuestAudioManager : MonoBehaviour
{
    [Header("Global Quest Sounds")]
    [Tooltip("Played when a quest becomes 'Active'")]
    public AudioClip questAcceptedSound;

    [Tooltip("Played when a quest becomes 'Success'")]
    public AudioClip questCompletedSound;

    [Tooltip("Played when a quest becomes 'Failure'")]
    public AudioClip questFailedSound;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // Optional: Ensure the audio source doesn't play on awake and handles 2D UI sound properly
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    /// <summary>
    /// The Dialogue System automatically broadcasts this to the Dialogue Manager 
    /// whenever any quest changes its state.
    /// </summary>
    public void OnQuestStateChange(string questName)
    {
        // Retrieve the brand new state of the quest that just changed
        QuestState newState = QuestLog.GetQuestState(questName);

        switch (newState)
        {
            case QuestState.Active:
                PlaySound(questAcceptedSound);
                break;
            case QuestState.Success:
                PlaySound(questCompletedSound);
                break;
            case QuestState.Failure:
                PlaySound(questFailedSound);
                break;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}