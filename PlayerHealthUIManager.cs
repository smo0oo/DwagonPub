using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the main UI display for the currently active player's health bar.
/// </summary>
public class PlayerHealthUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The Image component used as the health bar fill. Its 'Image Type' must be set to 'Filled'.")]
    public Image healthFillImage;
    [Tooltip("The text displaying the current/max health values.")]
    public TextMeshProUGUI healthText;

    private Health currentHealthComponent;

    /// <summary>
    /// Called by the InventoryUIController to link this UI to the active player's Health component.
    /// </summary>
    public void DisplayHealth(Health newHealthComponent)
    {
        // Unsubscribe from the old player's events to prevent memory leaks.
        if (currentHealthComponent != null)
        {
            currentHealthComponent.OnHealthChanged -= UpdateHealthUI;
        }

        currentHealthComponent = newHealthComponent;

        // Subscribe to the new player's events.
        if (currentHealthComponent != null)
        {
            currentHealthComponent.OnHealthChanged += UpdateHealthUI;
            UpdateHealthUI(); // Initial update
        }
        else
        {
            ClearUI(); // Clear the UI if there's no active player.
        }
    }

    private void UpdateHealthUI()
    {
        if (currentHealthComponent == null || healthFillImage == null || healthText == null) return;

        // Calculate the fill amount as a percentage.
        if (currentHealthComponent.maxHealth > 0)
        {
            healthFillImage.fillAmount = (float)currentHealthComponent.currentHealth / currentHealthComponent.maxHealth;
        }
        else
        {
            healthFillImage.fillAmount = 0;
        }

        // Update the text display.
        healthText.text = $"{currentHealthComponent.currentHealth} / {currentHealthComponent.maxHealth}";
    }

    private void ClearUI()
    {
        if (healthFillImage != null) healthFillImage.fillAmount = 0;
        if (healthText != null) healthText.text = "0 / 0";
    }

    void OnDestroy()
    {
        // Ensure we unsubscribe when the UI manager is destroyed.
        if (currentHealthComponent != null)
        {
            currentHealthComponent.OnHealthChanged -= UpdateHealthUI;
        }
    }
}
