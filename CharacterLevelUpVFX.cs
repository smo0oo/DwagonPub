using UnityEngine;
using UnityEngine.VFX; // Added for VFX Graph support
using System.Collections;

public class CharacterLevelUpVFX : MonoBehaviour
{
    [Header("VFX References")]
    [Tooltip("The child GameObject containing the Visual Effect component. It will be toggled on/off.")]
    public GameObject vfxRootObject;

    [Tooltip("The Visual Effect component (VFX Graph) to trigger.")]
    public VisualEffect mainVFX;

    [Header("Settings")]
    public float duration = 3.0f;
    [Tooltip("Delay before turning the VFX off? (0 = immediate off after duration)")]
    public bool autoDisable = true;

    void Start()
    {
        // Ensure VFX starts disabled
        if (vfxRootObject != null) vfxRootObject.SetActive(false);

        // Subscribe to the Party Manager
        if (PartyManager.instance != null)
        {
            PartyManager.instance.OnLevelUp += PlayLevelUpEffect;
        }
    }

    void OnDestroy()
    {
        if (PartyManager.instance != null)
        {
            PartyManager.instance.OnLevelUp -= PlayLevelUpEffect;
        }
    }

    private void PlayLevelUpEffect()
    {
        StopAllCoroutines(); // Reset timer if multiple level ups happen quickly
        StartCoroutine(LevelUpRoutine());
    }

    private IEnumerator LevelUpRoutine()
    {
        if (vfxRootObject != null)
        {
            vfxRootObject.SetActive(true);
        }

        if (mainVFX != null)
        {
            // Reset and Play the VFX Graph
            mainVFX.Reinit();
            mainVFX.Play();
        }

        yield return new WaitForSeconds(duration);

        if (autoDisable)
        {
            if (mainVFX != null) mainVFX.Stop();
            if (vfxRootObject != null) vfxRootObject.SetActive(false);
        }
    }
}