using UnityEngine;
using System.Collections.Generic;

public class LootLabelManager : MonoBehaviour
{
    public static LootLabelManager instance;

    [Header("Configuration")]
    [Tooltip("Assign the LootLabel prefab here.")]
    public GameObject labelPrefab;

    [Tooltip("Assign the Canvas (Screen Space - Overlay) that will hold these labels.")]
    public Transform labelCanvasParent;

    [Tooltip("Height of the label in pixels. Used for stacking calculations.")]
    public float labelHeight = 30f;

    [Tooltip("Pixels between stacked labels.")]
    public float stackSpacing = 2f;

    [Tooltip("Distance from camera at which labels vanish.")]
    public float maxViewDistance = 500f;

    [Header("Rarity Colors")]
    public Color commonColor = Color.white;
    public Color uncommonColor = Color.green;
    public Color rareColor = new Color(0.2f, 0.4f, 1f);
    public Color epicColor = new Color(0.6f, 0f, 0.8f);
    public Color legendaryColor = new Color(1f, 0.5f, 0f);

    // Internal Lists
    private List<WorldItem> activeItems = new List<WorldItem>();
    private List<LootLabel> activeLabels = new List<LootLabel>();
    private Camera mainCam;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void Start()
    {
        mainCam = Camera.main;
    }

    /// <summary>
    /// Registers a new item OR updates the text of an existing one.
    /// </summary>
    public void RegisterOrUpdateItem(WorldItem item)
    {
        if (activeItems.Contains(item))
        {
            // Already registered? Just update the text/color to match current data
            int index = activeItems.IndexOf(item);
            if (index >= 0 && index < activeLabels.Count)
            {
                LootLabel label = activeLabels[index];
                if (label != null)
                {
                    Color rColor = GetRarityColor(item.itemData);
                    label.Setup(item, rColor);
                }
            }
        }
        else
        {
            // New item
            activeItems.Add(item);
            CreateLabel(item);
        }
    }

    // Kept for backward compatibility, but redirects to the new logic
    public void RegisterItem(WorldItem item) => RegisterOrUpdateItem(item);

    public void UnregisterItem(WorldItem item)
    {
        if (activeItems.Contains(item))
        {
            int index = activeItems.IndexOf(item);

            activeItems.RemoveAt(index);

            if (index < activeLabels.Count)
            {
                LootLabel label = activeLabels[index];
                if (label != null) Destroy(label.gameObject);
                activeLabels.RemoveAt(index);
            }
        }
    }

    private void CreateLabel(WorldItem item)
    {
        if (labelPrefab == null || labelCanvasParent == null) return;

        GameObject obj = Instantiate(labelPrefab, labelCanvasParent);
        LootLabel label = obj.GetComponent<LootLabel>();

        if (label != null)
        {
            Color rColor = GetRarityColor(item.itemData);
            label.Setup(item, rColor);
            activeLabels.Add(label);
        }
    }

    private Color GetRarityColor(ItemData data)
    {
        if (data == null || data.stats == null) return commonColor;

        switch (data.stats.rarity)
        {
            case ItemStats.Rarity.Common: return commonColor;
            case ItemStats.Rarity.Uncommon: return uncommonColor;
            case ItemStats.Rarity.Rare: return rareColor;
            case ItemStats.Rarity.Epic: return epicColor;
            case ItemStats.Rarity.Legendary: return legendaryColor;
            default: return commonColor;
        }
    }

    void LateUpdate()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        List<LabelPositionData> screenData = new List<LabelPositionData>();

        for (int i = 0; i < activeLabels.Count; i++)
        {
            if (activeItems[i] == null)
            {
                if (activeLabels[i] != null) activeLabels[i].SetVisible(false);
                continue;
            }

            Vector3 worldPos = activeItems[i].transform.position;
            float dist = Vector3.Distance(mainCam.transform.position, worldPos);

            // Viewport Check (0-1 space)
            Vector3 viewportPos = mainCam.WorldToViewportPoint(worldPos);

            bool isInFront = viewportPos.z > 0;
            bool isOnScreenX = viewportPos.x > -0.1f && viewportPos.x < 1.1f;
            bool isOnScreenY = viewportPos.y > -0.1f && viewportPos.y < 1.1f;
            bool isWithinDist = dist < maxViewDistance;

            if (isInFront && isOnScreenX && isOnScreenY && isWithinDist)
            {
                activeLabels[i].SetVisible(true);
                Vector3 screenPos = mainCam.ViewportToScreenPoint(viewportPos);

                screenData.Add(new LabelPositionData
                {
                    label = activeLabels[i],
                    idealPos = screenPos,
                    currentPos = screenPos,
                    distance = dist
                });
            }
            else
            {
                activeLabels[i].SetVisible(false);
            }
        }

        // Grid Sort Logic
        screenData.Sort((a, b) => a.idealPos.y.CompareTo(b.idealPos.y));

        for (int i = 0; i < screenData.Count; i++)
        {
            for (int j = i + 1; j < screenData.Count; j++)
            {
                LabelPositionData lower = screenData[i];
                LabelPositionData upper = screenData[j];

                if (Mathf.Abs(lower.currentPos.x - upper.currentPos.x) < 100f)
                {
                    float verticalDist = upper.currentPos.y - lower.currentPos.y;
                    if (verticalDist < labelHeight + stackSpacing)
                    {
                        float pushAmount = (labelHeight + stackSpacing) - verticalDist;
                        upper.currentPos.y += pushAmount;
                    }
                }
            }
        }

        foreach (var data in screenData)
        {
            data.label.UpdatePosition(data.currentPos);
        }
    }

    private class LabelPositionData
    {
        public LootLabel label;
        public Vector3 idealPos;
        public Vector3 currentPos;
        public float distance;
    }
}