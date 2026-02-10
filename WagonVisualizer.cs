using UnityEngine;
using System.Collections.Generic;

public class WagonVisualizer : MonoBehaviour
{
    [Header("Anchors")]
    [Tooltip("Socket for the left wheels.")]
    public Transform wheelAnchorL;
    [Tooltip("Socket for the right wheels.")]
    public Transform wheelAnchorR;
    [Tooltip("Socket for the main chassis/body.")]
    public Transform chassisAnchor;
    [Tooltip("Socket for the roof/cover.")]
    public Transform coverAnchor;
    [Tooltip("Socket for lanterns/lights.")]
    public Transform lanternAnchor;
    [Tooltip("Socket for storage chests (rear/side).")]
    public Transform storageAnchor;
    [Tooltip("Socket for defensive attachments.")]
    public Transform defenseAnchor;

    private Dictionary<WagonUpgradeType, Transform> typeToAnchor;

    void Awake()
    {
        // Map types to anchors for easy lookup
        typeToAnchor = new Dictionary<WagonUpgradeType, Transform>
        {
            { WagonUpgradeType.Wheel, null }, // Handled specially for L/R
            { WagonUpgradeType.Chassis, chassisAnchor },
            { WagonUpgradeType.Cover, coverAnchor },
            { WagonUpgradeType.Lantern, lanternAnchor },
            { WagonUpgradeType.Storage, storageAnchor },
            { WagonUpgradeType.Defense, defenseAnchor }
        };
    }

    void Start()
    {
        // Subscribe to changes
        if (WagonManager.instance != null)
        {
            WagonManager.instance.OnWagonUpgradesChanged += RefreshVisuals;
            RefreshVisuals(); // Initial load
        }
    }

    void OnDestroy()
    {
        if (WagonManager.instance != null)
        {
            WagonManager.instance.OnWagonUpgradesChanged -= RefreshVisuals;
        }
    }

    public void RefreshVisuals()
    {
        if (WagonManager.instance == null) return;

        // 1. Handle Wheels (Special case: Left & Right)
        WagonUpgradeData wheelData = WagonManager.instance.GetInstalledUpgrade(WagonUpgradeType.Wheel);
        UpdateSocket(wheelAnchorL, wheelData);
        UpdateSocket(wheelAnchorR, wheelData);

        // 2. Handle Generic Sockets
        foreach (var kvp in typeToAnchor)
        {
            if (kvp.Key == WagonUpgradeType.Wheel) continue; // Already done
            if (kvp.Value == null) continue; // No anchor assigned

            WagonUpgradeData data = WagonManager.instance.GetInstalledUpgrade(kvp.Key);
            UpdateSocket(kvp.Value, data);
        }
    }

    private void UpdateSocket(Transform anchor, WagonUpgradeData data)
    {
        if (anchor == null) return;

        // Clear existing visuals
        foreach (Transform child in anchor)
        {
            Destroy(child.gameObject);
        }

        // Spawn new visual
        if (data != null && data.visualPrefab != null)
        {
            GameObject obj = Instantiate(data.visualPrefab, anchor);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            // Fix Layer (Ensure it matches the Wagon, e.g. "Player" or "WorldMap")
            SetLayerRecursively(obj, gameObject.layer);
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}