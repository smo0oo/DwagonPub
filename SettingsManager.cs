using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Linq;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager instance;

    [Header("References")]
    [Tooltip("Assign your MainMixer here")]
    public AudioMixer audioMixer;

    // Store all raw supported resolutions from the hardware
    private Resolution[] allResolutions;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Grab every resolution the monitor supports
        allResolutions = Screen.resolutions;
    }

    void Start()
    {
        ApplyLoadedSettings();
    }

    /// <summary>
    /// Returns unique width x height combinations (e.g., 1920x1080), filtering out duplicates caused by refresh rates.
    /// </summary>
    public List<Vector2Int> GetUniqueResolutions()
    {
        if (allResolutions == null) allResolutions = Screen.resolutions;

        return allResolutions
            .Where(r => r.width >= 1280 && r.height >= 720) // Filter out tiny resolutions
            .Select(r => new Vector2Int(r.width, r.height))
            .Distinct()
            .OrderBy(v => v.x).ThenBy(v => v.y)
            .ToList();
    }

    /// <summary>
    /// Returns available refresh rates for a specific resolution, sorted high to low.
    /// </summary>
    public List<int> GetRefreshRatesForResolution(int width, int height)
    {
        if (allResolutions == null) allResolutions = Screen.resolutions;

        return allResolutions
            .Where(r => r.width == width && r.height == height)
            .Select(r => (int)Mathf.Round((float)r.refreshRateRatio.value))
            .Distinct()
            .OrderByDescending(hz => hz) // Sort highest to lowest
            .ToList();
    }

    public void ApplyLoadedSettings()
    {
        // Ensure data exists
        SaveData data = SaveManager.instance.CurrentSaveData;
        if (data == null)
        {
            data = new SaveData(); // Uses the defaults defined in SaveData.cs (1920x1080 @ 60)
            SaveManager.instance.CurrentSaveData = data;
        }

        // 1. Setup Screen Mode
        FullScreenMode mode = FullScreenMode.FullScreenWindow;
        switch (data.fullscreenMode)
        {
            case 0: mode = FullScreenMode.ExclusiveFullScreen; break;
            case 1: mode = FullScreenMode.FullScreenWindow; break;
            case 2: mode = FullScreenMode.Windowed; break;
        }

        // 2. Validate Resolution
        // If the saved resolution is not supported by this monitor, fallback to current screen res
        bool isValid = allResolutions.Any(r => r.width == data.resolutionWidth && r.height == data.resolutionHeight);

        if (!isValid)
        {
            data.resolutionWidth = Screen.currentResolution.width;
            data.resolutionHeight = Screen.currentResolution.height;
            // Fallback Hz as well
            data.refreshRate = (int)Mathf.Round((float)Screen.currentResolution.refreshRateRatio.value);
        }

        // 3. Apply Resolution & Refresh Rate
        Screen.SetResolution(data.resolutionWidth, data.resolutionHeight, mode, new RefreshRate() { numerator = (uint)data.refreshRate, denominator = 1 });

        // 4. Quality & Audio
        QualitySettings.SetQualityLevel(data.graphicsQualityIndex, true);
        SetMixerVolume("MasterVolume", data.masterVolume);
        SetMixerVolume("MusicVolume", data.musicVolume);
        SetMixerVolume("SFXVolume", data.sfxVolume);
    }

    public void ApplyVideoSettings(int width, int height, int hz, int modeIndex)
    {
        FullScreenMode mode = FullScreenMode.FullScreenWindow;
        switch (modeIndex)
        {
            case 0: mode = FullScreenMode.ExclusiveFullScreen; break;
            case 1: mode = FullScreenMode.FullScreenWindow; break;
            case 2: mode = FullScreenMode.Windowed; break;
        }

        Screen.SetResolution(width, height, mode, new RefreshRate() { numerator = (uint)hz, denominator = 1 });

        // Save
        SaveManager.instance.CurrentSaveData.resolutionWidth = width;
        SaveManager.instance.CurrentSaveData.resolutionHeight = height;
        SaveManager.instance.CurrentSaveData.refreshRate = hz;
        SaveManager.instance.CurrentSaveData.fullscreenMode = modeIndex;
        SaveManager.instance.SaveGame();
    }

    public void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        SaveManager.instance.CurrentSaveData.graphicsQualityIndex = index;
        SaveManager.instance.SaveGame();
    }

    public void SetVolume(string parameterName, float linearValue)
    {
        SetMixerVolume(parameterName, linearValue);

        if (parameterName == "MasterVolume") SaveManager.instance.CurrentSaveData.masterVolume = linearValue;
        else if (parameterName == "MusicVolume") SaveManager.instance.CurrentSaveData.musicVolume = linearValue;
        else if (parameterName == "SFXVolume") SaveManager.instance.CurrentSaveData.sfxVolume = linearValue;

        SaveManager.instance.SaveGame();
    }

    private void SetMixerVolume(string param, float linear)
    {
        // Logarithmic conversion for natural volume fading
        float db = linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
        if (audioMixer != null) audioMixer.SetFloat(param, db);
    }
}