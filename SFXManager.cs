using UnityEngine;
using UnityEngine.Audio;

public class SFXManager : MonoBehaviour
{
    public static SFXManager instance;

    [Tooltip("Drag your SFX Mixer Group here so all spawned 3D sounds obey the options menu!")]
    public AudioMixerGroup sfxMixerGroup;

    [Header("Global 3D Falloff Settings")]
    [Tooltip("Distance at which the sound starts to fade. Increase this to keep sounds at 100% volume further away.")]
    public float globalMinDistance = 5f;

    [Tooltip("Distance at which the sound becomes completely silent.")]
    public float globalMaxDistance = 40f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // A drop-in replacement for AudioSource.PlayClipAtPoint that respects Audio Mixers!
    public static void PlayAtPoint(AudioClip clip, Vector3 position, float spatialBlend = 1.0f)
    {
        if (clip == null) return;

        GameObject go = new GameObject("SFX_" + clip.name);
        go.transform.position = position;

        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = spatialBlend; // 1.0 = Fully 3D (fades with distance)

        // --- AAA FIX: Set the custom Min and Max distances! ---
        source.rolloffMode = AudioRolloffMode.Linear;
        if (instance != null)
        {
            source.minDistance = instance.globalMinDistance;
            source.maxDistance = instance.globalMaxDistance;
        }
        else
        {
            // Fallbacks just in case the manager isn't in the scene yet
            source.minDistance = 5f;
            source.maxDistance = 40f;
        }
        // ------------------------------------------------------

        if (instance != null && instance.sfxMixerGroup != null)
        {
            source.outputAudioMixerGroup = instance.sfxMixerGroup;
        }

        source.Play();
        Destroy(go, clip.length + 0.1f); // Clean up the temp object automatically
    }
}