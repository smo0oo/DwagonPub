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

    [Header("Keyboard Control")]
    public float keyStepAmount = 1f;

    private DomeController domeController;
    private bool isUpdatingProgrammatically = false; // AAA Safety Flag

    void Start()
    {
        if (domePowerSlider != null) domePowerSlider.gameObject.SetActive(false);
        if (currentPowerText != null) currentPowerText.gameObject.SetActive(false);
        if (healthBarFill != null) healthBarFill.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (mitigationFill != null) mitigationFill.gameObject.SetActive(false);
        if (mitigationText != null) mitigationText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (domePowerSlider != null && domePowerSlider.gameObject.activeInHierarchy)
        {
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                domePowerSlider.value = Mathf.Clamp(domePowerSlider.value + keyStepAmount, domePowerSlider.minValue, domePowerSlider.maxValue);
            }
            else if (Input.GetKeyDown(KeyCode.PageDown))
            {
                domePowerSlider.value = Mathf.Clamp(domePowerSlider.value - keyStepAmount, domePowerSlider.minValue, domePowerSlider.maxValue);
            }
        }
    }

    public void InitializeAndShow(DomeController controller)
    {
        domeController = controller;
        if (domeController == null) return;

        SetElementsActive(true);

        if (domePowerSlider != null)
        {
            domePowerSlider.onValueChanged.RemoveAllListeners();
            domePowerSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }
    }

    private void SetElementsActive(bool active)
    {
        if (domePowerSlider != null) domePowerSlider.gameObject.SetActive(active);
        if (currentPowerText != null) currentPowerText.gameObject.SetActive(active);
        if (healthBarFill != null) healthBarFill.gameObject.SetActive(active);
        if (healthText != null) healthText.gameObject.SetActive(active);
        if (mitigationFill != null) mitigationFill.gameObject.SetActive(active);
        if (mitigationText != null) mitigationText.gameObject.SetActive(active);
    }

    public void UpdateSliderRange(float min, float max)
    {
        if (domePowerSlider == null) return;

        isUpdatingProgrammatically = true; // Lock events

        domePowerSlider.minValue = min;
        domePowerSlider.maxValue = max;

        // Ensure value is within bounds
        if (domePowerSlider.value < min) domePowerSlider.value = min;
        else if (domePowerSlider.value > max) domePowerSlider.value = max;

        isUpdatingProgrammatically = false; // Unlock
    }

    public void UpdateSliderValue(float value)
    {
        if (domePowerSlider != null)
        {
            isUpdatingProgrammatically = true; // Lock events
            domePowerSlider.SetValueWithoutNotify(value);
            UpdatePowerText(value);
            isUpdatingProgrammatically = false; // Unlock
        }
    }

    private void OnSliderValueChanged(float value)
    {
        // AAA FIX: Stop Feedback Loops
        if (isUpdatingProgrammatically) return;

        if (domeController != null)
        {
            domeController.UpdateDomePower(value);
        }
        UpdatePowerText(value);
    }

    private void UpdatePowerText(float value)
    {
        if (currentPowerText != null && domePowerSlider != null)
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
            healthBarFill.fillAmount = (maxHealth > 0) ? (float)currentHealth / maxHealth : 0;
        }
        if (healthText != null)
        {
            healthText.text = $"{currentHealth} / {maxHealth}";
        }
    }
}