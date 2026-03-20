using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public struct SceneMusicTrack
{
    public SceneType sceneType;
    public AudioClip musicTrack;
}

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    public static BackgroundMusic instance;

    [Header("Track Configuration")]
    [Tooltip("Map your SceneTypes to specific AudioClips here.")]
    public List<SceneMusicTrack> trackPlaylist = new List<SceneMusicTrack>();

    [Tooltip("How long it takes to fade out the old track and fade in the new one.")]
    public float fadeDuration = 1.0f;

    private AudioSource audioSource;
    private Coroutine currentFadeRoutine;
    private float maxSourceVolume = 1.0f; // Stores the baseline volume from the Inspector

    void Awake()
    {
        // Ensure only one music player exists in the entire game
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        maxSourceVolume = audioSource.volume;
        audioSource.loop = true;
    }

    void OnEnable()
    {
        // Listen for Unity's native scene change event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. Find the SceneInfo in the newly loaded scene
        SceneInfo sceneInfo = Object.FindFirstObjectByType<SceneInfo>();

        if (sceneInfo != null)
        {
            // 2. Look for a matching track for this SceneType
            foreach (var trackData in trackPlaylist)
            {
                if (trackData.sceneType == sceneInfo.type)
                {
                    PlayTrack(trackData.musicTrack);
                    return; // Found our track, exit the loop
                }
            }
        }
    }

    public void PlayTrack(AudioClip newClip)
    {
        // Don't restart or fade if the scene uses the exact same track we are already playing!
        if (audioSource.clip == newClip) return;

        // Stop any ongoing crossfades before starting a new one
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);

        // Start the smooth crossfade
        currentFadeRoutine = StartCoroutine(CrossfadeToNewTrack(newClip));
    }

    private IEnumerator CrossfadeToNewTrack(AudioClip newClip)
    {
        // --- Fade OUT the old track ---
        if (audioSource.isPlaying)
        {
            float startVolume = audioSource.volume;
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                // We manipulate the AudioSource volume directly. 
                // The AudioMixer (controlled by Options) will still apply its math on top of this!
                audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
                yield return null;
            }
        }

        audioSource.volume = 0f;
        audioSource.clip = newClip;

        // --- Fade IN the new track ---
        if (newClip != null)
        {
            audioSource.Play();
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                audioSource.volume = Mathf.Lerp(0f, maxSourceVolume, t / fadeDuration);
                yield return null;
            }
            audioSource.volume = maxSourceVolume;
        }
        else
        {
            // If no track was assigned, just stop playing
            audioSource.Stop();
        }
    }
}