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

    [Header("UI Sorting")]
    [Tooltip("The Sorting Order for the Loot Canvas. Lower this (e.g. 5) to make it draw BEHIND windows like Inventory (usually 10+).")]
    public int canvasSortOrder = 5; // [ADDED] Automatic Fix

    [Header("Rarity Colors (Base LDR)")]
    public Color commonColor = Color.white;
    public Color uncommonColor = Color.green;
    public Color rareColor = new Color(0.2f, 0.4f, 1f);
    public Color epicColor = new Color(0.6f, 0f, 0.8f);
    public Color legendaryColor = new Color(1f, 0.5f, 0f);

    [Header("HDR Settings")]
    [Tooltip("Multiplier applied to the Rarity Color to create an HDR effect (Glow).")]
    public float hdrIntensity = 5.0f;

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

        // --- AUTOMATIC SORT ORDER FIX ---
        // Try to find the canvas on the assigned parent, or this object
        Canvas targetCanvas = null;

        if (labelCanvasParent != null)
        {
            targetCanvas = labelCanvasParent.GetComponent<Canvas>();
            // If parent is just a panel, look up for the root canvas
            if (targetCanvas == null) targetCanvas = labelCanvasParent.GetComponentInParent<Canvas>();
        }
        else
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas != null)
        {
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = canvasSortOrder;
            Debug.Log($"[LootLabelManager] Auto-set Canvas Sorting Order to {canvasSortOrder}");
        }
        else
        {
            Debug.LogWarning("[LootLabelManager] Could not find a Canvas to apply Sorting Order! Labels might draw on top of UI.");
        }
        // --------------------------------
    }

    public void RegisterOrUpdateItem(WorldItem item)
    {
        if (activeItems.Contains(item))
        {
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
            activeItems.Add(item);
            CreateLabel(item);
        }
    }

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

    public Color GetRarityColor(ItemData data)
    {
        Color baseColor = commonColor;

        if (data != null && data.stats != null)
        {
            switch (data.stats.rarity)
            {
                case ItemStats.Rarity.Common: baseColor = commonColor; break;
                case ItemStats.Rarity.Uncommon: baseColor = uncommonColor; break;
                case ItemStats.Rarity.Rare: baseColor = rareColor; break;
                case ItemStats.Rarity.Epic: baseColor = epicColor; break;
                case ItemStats.Rarity.Legendary: baseColor = legendaryColor; break;
            }
        }

        // Apply HDR Boost
        return new Color(baseColor.r * hdrIntensity, baseColor.g * hdrIntensity, baseColor.b * hdrIntensity, baseColor.a);
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