using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the UI display for the player's mana bar using a filled image.
/// </summary>
public class ManaUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The Image component used as the mana bar. Its 'Image Type' must be set to 'Filled'.")]
    public Image manaFillImage;
    [Tooltip("The text displaying the current/max mana values.")]
    public TextMeshProUGUI manaText;

    private PlayerStats currentPlayerStats;

    /// <summary>
    /// Called by the InventoryUIController to link this UI to the active player.
    /// </summary>
    public void DisplayMana(PlayerStats newPlayerStats)
    {
        if (currentPlayerStats != null)
        {
            currentPlayerStats.OnManaChanged -= UpdateManaUI;
        }

        currentPlayerStats = newPlayerStats;

        if (currentPlayerStats != null)
        {
            currentPlayerStats.OnManaChanged += UpdateManaUI;
            UpdateManaUI();
        }
        else
        {
            ClearUI();
        }
    }

    private void UpdateManaUI()
    {
        if (currentPlayerStats == null || manaFillImage == null || manaText == null) return;

        if (currentPlayerStats.maxMana > 0)
        {
            manaFillImage.fillAmount = currentPlayerStats.currentMana / currentPlayerStats.maxMana;
        }
        else
        {
            manaFillImage.fillAmount = 0;
        }

        // --- THIS IS THE FIX ---
        // Use Mathf.FloorToInt to display the current mana as a whole number,
        // preventing floating point numbers like "5.666" from appearing in the UI.
        manaText.text = $"{Mathf.FloorToInt(currentPlayerStats.currentMana)} / {currentPlayerStats.maxMana}";
    }

    private void ClearUI()
    {
        if (manaFillImage != null) manaFillImage.fillAmount = 0;
        if (manaText != null) manaText.text = "0 / 0";
    }
}
