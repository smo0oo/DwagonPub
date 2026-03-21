using UnityEngine;
using UnityEngine.Audio;

public class SFXManager : MonoBehaviour
{
    public static SFXManager instance;

    [Tooltip("Drag your SFX Mixer Group here so all spawned 3D sounds obey the options menu!")]
    public AudioMixerGroup sfxMixerGroup;

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
        source.rolloffMode = AudioRolloffMode.Linear;
        source.maxDistance = 30f; // How far away you can hear the sound

        if (instance != null && instance.sfxMixerGroup != null)
        {
            source.outputAudioMixerGroup = instance.sfxMixerGroup;
        }

        source.Play();
        Destroy(go, clip.length + 0.1f); // Clean up the temp object automatically
    }
}