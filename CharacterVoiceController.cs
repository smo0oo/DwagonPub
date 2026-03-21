using UnityEngine;
using UnityEngine.Audio;

public enum VoiceEffort
{
    None,
    LightAttack,
    HeavyAttack,
    MagicCast
}

[RequireComponent(typeof(AudioSource))]
public class CharacterVoiceController : MonoBehaviour
{
    [Header("Audio Routing")]
    public AudioMixerGroup sfxMixerGroup;

    [Header("Voice Lines")]
    public AudioClip[] lightAttackGrunts;
    public AudioClip[] heavyAttackGrunts;
    public AudioClip[] magicCastChants;

    [Header("Randomization")]
    [Range(0.8f, 1.2f)] public float pitchMin = 0.95f;
    [Range(0.8f, 1.2f)] public float pitchMax = 1.05f;

    private AudioSource voiceSource;

    void Awake()
    {
        voiceSource = GetComponent<AudioSource>();
        voiceSource.spatialBlend = 1f; // 3D sound

        if (sfxMixerGroup != null)
            voiceSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    public void PlayEffort(VoiceEffort effort)
    {
        AudioClip[] clipsToPlay = null;

        switch (effort)
        {
            case VoiceEffort.LightAttack: clipsToPlay = lightAttackGrunts; break;
            case VoiceEffort.HeavyAttack: clipsToPlay = heavyAttackGrunts; break;
            case VoiceEffort.MagicCast: clipsToPlay = magicCastChants; break;
        }

        if (clipsToPlay != null && clipsToPlay.Length > 0)
        {
            AudioClip clip = clipsToPlay[Random.Range(0, clipsToPlay.Length)];
            voiceSource.pitch = Random.Range(pitchMin, pitchMax);
            voiceSource.PlayOneShot(clip);
        }
    }
}