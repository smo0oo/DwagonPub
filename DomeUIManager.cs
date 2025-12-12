using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DomeUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Slider domePowerSlider;
    public TextMeshProUGUI currentPowerText;

    [Header("Health Display")]
    public Image healthBarFill;
    public TextMeshProUGUI healthText;

    [Header("Mitigation Display")]
    public Image mitigationFill;
    public TextMeshProUGUI mitigationText;

    // --- NEW VARIABLE ADDED HERE ---
    [Header("Keyboard Control")]
    [Tooltip("How much to increase/decrease the dome power when using PageUp/PageDown keys.")]
    public float keyStepAmount = 1f;

    private DomeController domeController;

    void Start()
    {
        if (domePowerSlider != null) domePowerSlider.gameObject.SetActive(false);
        if (currentPowerText != null) currentPowerText.gameObject.SetActive(false);
        if (healthBarFill != null) healthBarFill.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (mitigationFill != null) mitigationFill.gameObject.SetActive(false);
        if (mitigationText != null) mitigationText.gameObject.SetActive(false);
    }

    // --- UPDATE METHOD MODIFIED HERE ---
    void Update()
    {
        // Only process keyboard input if the slider is active and usable.
        if (domePowerSlider != null && domePowerSlider.gameObject.activeInHierarchy)
        {
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                // Increase the slider's value by the step amount, clamping it to the max value.
                domePowerSlider.value = Mathf.Clamp(domePowerSlider.value + keyStepAmount, domePowerSlider.minValue, domePowerSlider.maxValue);
            }
            else if (Input.GetKeyDown(KeyCode.PageDown))
            {
                // Decrease the slider's value by the step amount, clamping it to the min value.
                domePowerSlider.value = Mathf.Clamp(domePowerSlider.value - keyStepAmount, domePowerSlider.minValue, domePowerSlider.maxValue);
            }
        }
    }

    public void InitializeAndShow(DomeController controller)
    {
        domeController = controller;
        if (domeController == null) return;

        if (domePowerSlider != null) domePowerSlider.gameObject.SetActive(true);
        if (currentPowerText != null) currentPowerText.gameObject.SetActive(true);
        if (healthBarFill != null) healthBarFill.gameObject.SetActive(true);
        if (healthText != null) healthText.gameObject.SetActive(true);
        if (mitigationFill != null) mitigationFill.gameObject.SetActive(true);
        if (mitigationText != null) mitigationText.gameObject.SetActive(true);

        if (domePowerSlider != null)
        {
            domePowerSlider.onValueChanged.RemoveAllListeners();
            domePowerSlider.onValueChanged.AddListener(UpdateDome);
        }
    }

    public void UpdateSliderRange(float min, float max)
    {
        if (domePowerSlider == null) return;
        domePowerSlider.minValue = min;
        domePowerSlider.maxValue = max;
        domePowerSlider.value = min;
        UpdateDome(min);
    }

    private void UpdateDome(float value)
    {
        if (domeController != null)
        {
            domeController.UpdateDomePower(value);
        }
        if (currentPowerText != null)
        {
            currentPowerText.text = $"Dome Power: {(int)value} / {(int)domePowerSlider.maxValue}";
        }
    }

    public void UpdateMitigationUI(float mitigationPercent)
    {
        if (mitigationText != null)
        {
            mitigationText.text = $"Mitigation: {mitigationPercent * 100f:F1}%";
        }
        if (mitigationFill != null)
        {
            mitigationFill.fillAmount = Mathf.InverseLerp(0.25f, 0.75f, mitigationPercent) * 0.5f + 0.5f;
        }
    }

    public void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        if (healthBarFill != null)
        {
            if (maxHealth > 0)
            {
                healthBarFill.fillAmount = (float)currentHealth / maxHealth;
            }
            else
            {
                healthBarFill.fillAmount = 0;
            }
        }
        if (healthText != null)
        {
            healthText.text = $"{currentHealth} / {maxHealth}";
        }
    }
}