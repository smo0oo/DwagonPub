using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class OptionsMenuUI : MonoBehaviour
{
    public static OptionsMenuUI instance;

    [Header("Container")]
    public GameObject contentPanel;

    [Header("Video Controls")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown refreshRateDropdown; // --- NEW SEPARATE DROPDOWN ---
    public TMP_Dropdown fullscreenDropdown;
    public TMP_Dropdown qualityDropdown;

    [Header("Audio Controls")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Buttons")]
    public Button backButton;

    // Cache unique resolutions
    private List<Vector2Int> uniqueResolutions;
    private List<int> availableRefreshRates;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        if (contentPanel == null) contentPanel = gameObject;
    }

    void Start()
    {
        InitializeVideoControls();
        InitializeAudioControls();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseMenu);
        }
        CloseMenu();
    }

    public void OpenMenu()
    {
        UpdateUIValues();
        contentPanel.SetActive(true);
    }

    public void CloseMenu()
    {
        contentPanel.SetActive(false);
        // Optional: Notify InGameMenuController to show its buttons again
        if (InGameMenuController.instance != null && InGameMenuController.IsGamePaused)
        {
            InGameMenuController.instance.CloseOptions();
        }
    }

    private void InitializeVideoControls()
    {
        if (SettingsManager.instance == null) return;

        // 1. Populate Resolutions (Unique Sizes Only)
        uniqueResolutions = SettingsManager.instance.GetUniqueResolutions();

        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            List<string> options = new List<string>();
            foreach (var res in uniqueResolutions)
            {
                options.Add(res.x + " x " + res.y);
            }
            resolutionDropdown.AddOptions(options);

            // Link Listener
            resolutionDropdown.onValueChanged.AddListener(OnResolutionSelected);
        }

        // 2. Setup Refresh Rate Listener
        if (refreshRateDropdown != null)
        {
            refreshRateDropdown.onValueChanged.AddListener(OnRefreshRateSelected);
        }

        // 3. Setup Fullscreen
        if (fullscreenDropdown != null)
        {
            fullscreenDropdown.ClearOptions();
            fullscreenDropdown.AddOptions(new List<string> { "Exclusive Fullscreen", "Borderless Window", "Windowed" });
            fullscreenDropdown.onValueChanged.AddListener(OnFullscreenModeChanged);
        }

        // 4. Setup Quality
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.onValueChanged.AddListener((index) => SettingsManager.instance.SetQualityLevel(index));
        }
    }

    private void InitializeAudioControls()
    {
        if (SettingsManager.instance == null) return;

        if (masterSlider)
        {
            masterSlider.minValue = 0.0001f; masterSlider.maxValue = 1f;
            masterSlider.onValueChanged.AddListener((v) => SettingsManager.instance.SetVolume("MasterVolume", v));
        }
        if (musicSlider)
        {
            musicSlider.minValue = 0.0001f; musicSlider.maxValue = 1f;
            musicSlider.onValueChanged.AddListener((v) => SettingsManager.instance.SetVolume("MusicVolume", v));
        }
        if (sfxSlider)
        {
            sfxSlider.minValue = 0.0001f; sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener((v) => SettingsManager.instance.SetVolume("SFXVolume", v));
        }
    }

    private void UpdateUIValues()
    {
        if (SaveManager.instance == null || SaveManager.instance.CurrentSaveData == null) return;
        SaveData data = SaveManager.instance.CurrentSaveData;

        // Sliders
        if (masterSlider) masterSlider.value = data.masterVolume;
        if (musicSlider) musicSlider.value = data.musicVolume;
        if (sfxSlider) sfxSlider.value = data.sfxVolume;

        // Dropdowns
        if (fullscreenDropdown) fullscreenDropdown.value = data.fullscreenMode;
        if (qualityDropdown) qualityDropdown.value = data.graphicsQualityIndex;

        // Resolution Matching Logic
        if (resolutionDropdown != null && uniqueResolutions != null)
        {
            int foundIndex = uniqueResolutions.FindIndex(r => r.x == data.resolutionWidth && r.y == data.resolutionHeight);

            // Default to 1920x1080 if saved res not found
            if (foundIndex == -1)
                foundIndex = uniqueResolutions.FindIndex(r => r.x == 1920 && r.y == 1080);

            // Fallback to first available if 1920x1080 not found
            if (foundIndex == -1 && uniqueResolutions.Count > 0)
                foundIndex = 0;

            // Update Dropdown without triggering event loop
            resolutionDropdown.SetValueWithoutNotify(foundIndex);

            // Manually populate the Refresh Rate dropdown for this resolution
            UpdateRefreshRateDropdown(uniqueResolutions[foundIndex], data.refreshRate);
        }
    }

    // Triggered when Player picks a new Resolution
    public void OnResolutionSelected(int index)
    {
        Vector2Int selectedRes = uniqueResolutions[index];

        // Try to keep current Hz preference, or default to 60
        int currentHz = 60;
        if (SaveManager.instance.CurrentSaveData != null)
            currentHz = SaveManager.instance.CurrentSaveData.refreshRate;

        UpdateRefreshRateDropdown(selectedRes, currentHz);

        // Apply immediately
        ApplyVideoSettings();
    }

    // Triggered when Player picks a new Refresh Rate
    public void OnRefreshRateSelected(int index)
    {
        ApplyVideoSettings();
    }

    public void OnFullscreenModeChanged(int index)
    {
        ApplyVideoSettings();
    }

    private void UpdateRefreshRateDropdown(Vector2Int resolution, int preferredHz)
    {
        if (refreshRateDropdown == null) return;

        // Get valid Hz list for this resolution
        availableRefreshRates = SettingsManager.instance.GetRefreshRatesForResolution(resolution.x, resolution.y);

        refreshRateDropdown.ClearOptions();
        List<string> options = new List<string>();
        int targetIndex = 0;

        for (int i = 0; i < availableRefreshRates.Count; i++)
        {
            int hz = availableRefreshRates[i];
            options.Add(hz + " Hz");

            // Try to match exact preferred Hz
            if (hz == preferredHz) targetIndex = i;
        }

        // If exact Hz not found, look for 60Hz as a safe default
        if (availableRefreshRates[targetIndex] != preferredHz)
        {
            int index60 = availableRefreshRates.IndexOf(60);
            if (index60 != -1) targetIndex = index60;
        }

        refreshRateDropdown.AddOptions(options);
        refreshRateDropdown.SetValueWithoutNotify(targetIndex);
    }

    private void ApplyVideoSettings()
    {
        if (SettingsManager.instance == null) return;

        // Gather all values from UI
        Vector2Int res = uniqueResolutions[resolutionDropdown.value];
        int hz = availableRefreshRates[refreshRateDropdown.value];
        int mode = fullscreenDropdown.value;

        SettingsManager.instance.ApplyVideoSettings(res.x, res.y, hz, mode);
    }
}