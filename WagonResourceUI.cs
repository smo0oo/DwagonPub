using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class WagonResourceUI : MonoBehaviour
{
    [Header("Manager Reference")]
    public WagonResourceManager resourceManager;

    [Header("Rations UI")]
    public Slider rationsSlider;
    public TextMeshProUGUI rationsText;
    public Image rationsFillImage;
    [Tooltip("Color of the bar when starving.")]
    public Color starvationColor = Color.red;
    private Color originalRationsColor;

    [Header("Fuel UI")]
    public Slider fuelSlider;
    public TextMeshProUGUI fuelText;
    public Image fuelFillImage;
    [Tooltip("Color of the bar when out of fuel.")]
    public Color emptyFuelColor = Color.gray;
    private Color originalFuelColor;

    [Header("Integrity UI")]
    public Slider integritySlider;
    public TextMeshProUGUI integrityText;
    public Image integrityFillImage;
    public Color brokenColor = Color.black;
    private Color originalIntegrityColor;

    // Coroutine trackers for flashing effects
    private Coroutine rationsFlashRoutine;
    private Coroutine fuelFlashRoutine;

    void Start()
    {
        // 1. Auto-Find Manager (Cross-Scene Safety)
        if (resourceManager == null)
        {
            resourceManager = FindAnyObjectByType<WagonResourceManager>();
        }

        // 2. Store Original Colors
        if (rationsFillImage != null) originalRationsColor = rationsFillImage.color;
        if (fuelFillImage != null) originalFuelColor = fuelFillImage.color;
        if (integrityFillImage != null) originalIntegrityColor = integrityFillImage.color;

        // 3. Subscribe to Events via Code
        if (resourceManager != null)
        {
            // Update values whenever resources change
            resourceManager.OnResourcesChanged.AddListener(UpdateUI);

            // Trigger effects on critical events
            resourceManager.OnStarvationStarted.AddListener(OnStarvationVisuals);
            resourceManager.OnFuelDepleted.AddListener(OnFuelDepletedVisuals);
            resourceManager.OnWagonBroken.AddListener(OnWagonBrokenVisuals);

            // Initial visual update
            UpdateUI();
        }
        else
        {
            Debug.LogWarning("WagonResourceUI: Could not find WagonResourceManager in the scene.");
            gameObject.SetActive(false); // Hide UI if manager is missing
        }
    }

    void OnDestroy()
    {
        // Always unsubscribe to prevent memory leaks
        if (resourceManager != null)
        {
            resourceManager.OnResourcesChanged.RemoveListener(UpdateUI);
            resourceManager.OnStarvationStarted.RemoveListener(OnStarvationVisuals);
            resourceManager.OnFuelDepleted.RemoveListener(OnFuelDepletedVisuals);
            resourceManager.OnWagonBroken.RemoveListener(OnWagonBrokenVisuals);
        }
    }

    // --- Event Handlers ---

    private void OnStarvationVisuals()
    {
        if (rationsFlashRoutine != null) StopCoroutine(rationsFlashRoutine);
        rationsFlashRoutine = StartCoroutine(FlashBar(rationsFillImage, originalRationsColor, starvationColor));
        if (FloatingTextManager.instance != null)
            FloatingTextManager.instance.ShowEvent("<color=red>STARVATION!</color>", transform.position); // Position might need adjustment for UI
    }

    private void OnFuelDepletedVisuals()
    {
        if (fuelFlashRoutine != null) StopCoroutine(fuelFlashRoutine);
        fuelFlashRoutine = StartCoroutine(FlashBar(fuelFillImage, originalFuelColor, emptyFuelColor));
        if (FloatingTextManager.instance != null)
            FloatingTextManager.instance.ShowEvent("<color=blue>OUT OF FUEL!</color>", transform.position);
    }

    private void OnWagonBrokenVisuals()
    {
        if (integrityFillImage != null) integrityFillImage.color = brokenColor;
        // This is usually a Game Over state, handled by GameManager
    }

    public void UpdateUI()
    {
        if (resourceManager == null) return;

        // --- RATIONS ---
        if (rationsSlider != null)
        {
            rationsSlider.maxValue = resourceManager.maxRations;
            rationsSlider.value = resourceManager.currentRations;
        }
        if (rationsText != null)
        {
            rationsText.text = $"{Mathf.FloorToInt(resourceManager.currentRations)} / {resourceManager.maxRations}";
        }
        // Stop flashing if resources recovered
        if (resourceManager.currentRations > 0 && rationsFlashRoutine != null)
        {
            StopCoroutine(rationsFlashRoutine);
            rationsFlashRoutine = null;
            if (rationsFillImage != null) rationsFillImage.color = originalRationsColor;
        }

        // --- FUEL ---
        if (fuelSlider != null)
        {
            fuelSlider.maxValue = resourceManager.maxFuel;
            fuelSlider.value = resourceManager.currentFuel;
        }
        if (fuelText != null)
        {
            fuelText.text = $"{Mathf.FloorToInt(resourceManager.currentFuel)} / {resourceManager.maxFuel}";
        }
        // Stop flashing if resources recovered
        if (resourceManager.currentFuel > 0 && fuelFlashRoutine != null)
        {
            StopCoroutine(fuelFlashRoutine);
            fuelFlashRoutine = null;
            if (fuelFillImage != null) fuelFillImage.color = originalFuelColor;
        }

        // --- INTEGRITY ---
        if (integritySlider != null)
        {
            integritySlider.maxValue = resourceManager.maxIntegrity;
            integritySlider.value = resourceManager.currentIntegrity;
        }
        if (integrityText != null)
        {
            integrityText.text = $"{Mathf.FloorToInt(resourceManager.currentIntegrity)}%";
        }
    }

    private IEnumerator FlashBar(Image targetImage, Color normalColor, Color warningColor)
    {
        if (targetImage == null) yield break;

        while (true)
        {
            // Fade to warning
            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * 5f;
                targetImage.color = Color.Lerp(normalColor, warningColor, t);
                yield return null;
            }
            // Fade back
            t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * 5f;
                targetImage.color = Color.Lerp(warningColor, normalColor, t);
                yield return null;
            }
        }
    }
}