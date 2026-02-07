using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LootLabel : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI nameText;
    public Image backgroundImage;
    public CanvasGroup canvasGroup;
    public Button button; // Optional: If you want them clickable

    private WorldItem targetItem;
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(WorldItem item, Color rarityColor)
    {
        targetItem = item;

        if (nameText != null)
        {
            nameText.text = item.itemData.displayName;
            nameText.color = rarityColor;
        }

        // Optional: Tint background based on rarity (darkened)
        if (backgroundImage != null)
        {
            Color bg = Color.black;
            bg.a = 0.7f; // Fixed opacity
            backgroundImage.color = bg;
        }

        // Start visible immediately
        SetVisible(true);
    }

    public void UpdatePosition(Vector2 screenPos)
    {
        // Strictly set position. No smoothing, no lerping.
        transform.position = screenPos;
    }

    public void SetVisible(bool state)
    {
        // Instant toggle
        if (canvasGroup != null)
        {
            canvasGroup.alpha = state ? 1f : 0f;
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;
        }
        else
        {
            gameObject.SetActive(state);
        }
    }

    public WorldItem GetItem()
    {
        return targetItem;
    }

    // Connect this to the Button component on the Prefab
    public void OnClick()
    {
        if (targetItem != null)
        {
            // Logic to pick up the item
            targetItem.Interact(PartyManager.instance.ActivePlayer);
        }
    }
}