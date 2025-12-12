using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Required for Action

/// <summary>
/// Manages the UI window for splitting an item stack.
/// Now uses a callback Action to be reusable by any manager.
/// </summary>
public class StackSplitter : MonoBehaviour
{
    [Header("UI References")]
    public Slider amountSlider;
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI itemNameText;
    public Image itemIcon;
    public Button confirmButton;
    public Button cancelButton;

    private Action<int> onConfirmSplit;
    private int totalQuantity;

    /// <summary>
    /// Opens the window for a specific item stack.
    /// </summary>
    /// <param name="itemStack">The stack to be split.</param>
    /// <param name="confirmAction">The action to call when the user confirms the split amount.</param>
    public void Open(ItemStack itemStack, Action<int> confirmAction)
    {
        if (itemStack == null || itemStack.itemData == null || itemStack.quantity <= 1)
        {
            Close();
            return;
        }

        gameObject.SetActive(true);
        totalQuantity = itemStack.quantity;
        this.onConfirmSplit = confirmAction;

        // Configure UI elements
        itemNameText.text = itemStack.itemData.displayName;
        itemIcon.sprite = itemStack.itemData.icon;

        // Configure slider
        amountSlider.minValue = 1;
        amountSlider.maxValue = totalQuantity - 1;
        amountSlider.value = Mathf.FloorToInt(totalQuantity / 2f);
        OnSliderValueChanged();

        // Add listeners
        amountSlider.onValueChanged.AddListener(delegate { OnSliderValueChanged(); });
        confirmButton.onClick.AddListener(OnConfirmClicked);
        cancelButton.onClick.AddListener(Close);
    }

    private void OnSliderValueChanged()
    {
        amountText.text = $"Split: {amountSlider.value} / {totalQuantity - (int)amountSlider.value}";
    }

    private void OnConfirmClicked()
    {
        // Invoke the callback with the chosen amount
        onConfirmSplit?.Invoke((int)amountSlider.value);
        Close();
    }

    /// <summary>
    /// Closes the window and clears listeners.
    /// </summary>
    public void Close()
    {
        amountSlider.onValueChanged.RemoveAllListeners();
        confirmButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();
        onConfirmSplit = null;
        gameObject.SetActive(false);
    }
}
