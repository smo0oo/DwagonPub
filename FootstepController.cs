using UnityEngine;
using UnityEngine.Audio;

public class FootstepController : MonoBehaviour
{
    [Header("Audio Routing")]
    [Tooltip("Drag your SFX Audio Mixer Group here so footsteps obey the options menu.")]
    public AudioMixerGroup sfxMixerGroup;

    [Header("Footstep Assets")]
    public AudioClip[] footstepClips;

    [Header("Randomization (AAA Game Feel)")]
    [Range(0f, 1f)] public float volumeMin = 0.8f;
    [Range(0f, 1f)] public float volumeMax = 1.0f;

    [Range(0.5f, 1.5f)] public float pitchMin = 0.9f;
    [Range(0.5f, 1.5f)] public float pitchMax = 1.1f;

    [Header("3D Audio Settings (Distance Fade)")]
    public float minDistance = 2f;
    public float maxDistance = 15f;
    [Range(0f, 1f)] public float spatialBlend = 1.0f;

    [Header("Overlap Settings")]
    public int concurrentSounds = 3;

    [Header("Continuous Movement Loop (Cloth/Armor)")]
    public AudioClip movementLoopClip;
    [Range(0f, 1f)] public float maxLoopVolume = 0.5f;
    public float loopFadeSpeed = 5f;

    [Header("Party Audio Mixing")]
    [Tooltip("If true, this character's movement sounds will be dynamically quieter if they are NOT the actively controlled player.")]
    public bool duckVolumeIfNotActivePlayer = true;
    [Tooltip("Multiply the volume by this amount if this is an AI companion (0.5 = half volume).")]
    [Range(0f, 1f)] public float inactiveVolumeMultiplier = 0.5f;

    private AudioSource[] audioSources;
    private int currentSourceIndex = 0;

    private AudioSource loopSource;
    private float targetLoopVolume = 0f;

    void Awake()
    {
        // --- Setup Footstep Speakers (Round-Robin) ---
        audioSources = new AudioSource[concurrentSounds];

        for (int i = 0; i < concurrentSounds; i++)
        {
            AudioSource newSource = gameObject.AddComponent<AudioSource>();

            newSource.spatialBlend = spatialBlend;
            newSource.rolloffMode = AudioRolloffMode.Linear;
            newSource.minDistance = minDistance;
            newSource.maxDistance = maxDistance;

            newSource.playOnAwake = false;

            if (sfxMixerGroup != null)
            {
                newSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            audioSources[i] = newSource;
        }

        // --- Setup Movement Loop Speaker ---
        if (movementLoopClip != null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();

            loopSource.spatialBlend = spatialBlend;
            loopSource.rolloffMode = AudioRolloffMode.Linear;
            loopSource.minDistance = minDistance;
            loopSource.maxDistance = maxDistance;

            loopSource.loop = true;
            loopSource.clip = movementLoopClip;
            loopSource.volume = 0f;

            if (sfxMixerGroup != null)
            {
                loopSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            loopSource.Play();
        }
    }

    void Update()
    {
        if (loopSource != null)
        {
            loopSource.volume = Mathf.Lerp(loopSource.volume, targetLoopVolume, Time.deltaTime * loopFadeSpeed);
        }
    }

    // --- NEW: Dynamic Volume Check ---
    private float GetDynamicVolumeMultiplier()
    {
        if (duckVolumeIfNotActivePlayer && PartyManager.instance != null)
        {
            // If the Party Manager exists, and this specific GameObject is NOT the Active Player...
            if (PartyManager.instance.ActivePlayer != null && PartyManager.instance.ActivePlayer != transform.root.gameObject)
            {
                return inactiveVolumeMultiplier; // Duck the volume!
            }
        }
        return 1.0f; // Otherwise, play at full volume
    }

    public void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        AudioSource source = audioSources[currentSourceIndex];

        source.pitch = Random.Range(pitchMin, pitchMax);

        // Multiply our standard random volume by the dynamic active/inactive multiplier
        float baseVolume = Random.Range(volumeMin, volumeMax);
        source.volume = baseVolume * GetDynamicVolumeMultiplier();

        source.clip = clip;
        source.Play();

        currentSourceIndex = (currentSourceIndex + 1) % concurrentSounds;
    }

    public void SetMovementIntensity(float normalizedSpeed)
    {
        if (loopSource == null) return;

        // Apply the same active/inactive volume multiplier to the continuous cloth rustle
        targetLoopVolume = normalizedSpeed * maxLoopVolume * GetDynamicVolumeMultiplier();
    }
}