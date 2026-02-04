using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Reflection;

public class RuntimeDebugManager : MonoBehaviour
{
    public static RuntimeDebugManager instance;

    [Header("Selection Settings")]
    [Tooltip("Select the layer your Enemies AND Players are on. Set to 'Everything' if unsure.")]
    public LayerMask selectionLayer;

    [Tooltip("Button to select entities.")]
    public KeyCode selectionKey = KeyCode.Mouse4;

    public KeyCode modifierKey = KeyCode.None;
    public KeyCode toggleKey = KeyCode.F3;

    [Header("UI References")]
    public GameObject debugPanel;
    public TextMeshProUGUI entityNameText;
    public Slider healthSlider;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI stateText;
    public TextMeshProUGUI targetText;
    public TextMeshProUGUI cooldownsText;
    public RawImage graphImage;
    public TextMeshProUGUI perfStatsText;

    [Header("Graph Colors")]
    public Color graphBgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    public Color graphLineColor = Color.green;
    public Color graphSpikeColor = Color.red;

    private EntityDebugger selectedEntity;
    private Texture2D graphTexture;
    private Color[] clearColors;
    private const int GRAPH_WIDTH = 128;
    private const int GRAPH_HEIGHT = 64;
    private List<float> perfHistory = new List<float>();
    private const float MAX_GRAPH_MS = 2.0f;

    void Awake()
    {
        instance = this;
        InitializeGraph();
        if (debugPanel) debugPanel.SetActive(false);
    }

    void Update()
    {
        HandleInput();
        if (selectedEntity != null && debugPanel.activeSelf) UpdateUI();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(toggleKey)) if (debugPanel) debugPanel.SetActive(!debugPanel.activeSelf);

        bool modifierPressed = (modifierKey == KeyCode.None) || Input.GetKey(modifierKey);

        if (Input.GetKeyDown(selectionKey) && modifierPressed)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, selectionLayer))
            {
                EntityDebugger target = hit.collider.GetComponentInParent<EntityDebugger>();
                if (target != null) SelectEntity(target);
            }
        }
    }

    public void SelectEntity(EntityDebugger entity)
    {
        selectedEntity = entity;
        if (debugPanel) debugPanel.SetActive(true);
        if (entityNameText) entityNameText.text = entity.gameObject.name;
        perfHistory.Clear();
        InitializeGraph();
    }

    private void UpdateUI()
    {
        if (selectedEntity == null) return;

        // --- 1. HEALTH ---
        if (selectedEntity.healthComponent != null)
        {
            float hp = selectedEntity.healthComponent.currentHealth;
            float max = selectedEntity.healthComponent.maxHealth;
            if (healthSlider) healthSlider.value = hp / max;
            if (healthText) healthText.text = $"{hp:F0} / {max:F0}";
        }

        // --- 2. LOGIC STATE (Player vs Enemy) ---
        if (selectedEntity.playerMovement != null)
        {
            // PLAYER MODE
            if (stateText) stateText.text = $"Mode: {selectedEntity.playerMovement.currentMode}";

            string tName = selectedEntity.playerMovement.TargetObject != null ? selectedEntity.playerMovement.TargetObject.name : "None";
            if (targetText) targetText.text = $"Target: {tName}";

            UpdateGraph(selectedEntity.playerMovement.LastExecutionTimeMs);
        }
        else if (selectedEntity.aiComponent != null)
        {
            // ENEMY MODE
            FieldInfo stateField = typeof(EnemyAI).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance);
            string stateName = "Null";
            if (stateField != null)
            {
                var state = stateField.GetValue(selectedEntity.aiComponent);
                if (state != null) stateName = state.GetType().Name;
            }
            if (stateText) stateText.text = $"State: {stateName}";

            FieldInfo targetField = typeof(EnemyAI).GetField("currentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            string targetName = "None";
            if (targetField != null)
            {
                var target = targetField.GetValue(selectedEntity.aiComponent) as Transform;
                if (target != null) targetName = target.name;
            }
            if (targetText) targetText.text = $"Target: {targetName}";

            UpdateGraph(selectedEntity.aiComponent.LastExecutionTimeMs);
        }

        // --- 3. COOLDOWNS ---
        if (cooldownsText)
        {
            string cooldownString = "";
            Dictionary<Ability, float> cooldowns = null;

            // Try get Enemy Cooldowns
            if (selectedEntity.abilityHolder != null)
            {
                FieldInfo f = typeof(EnemyAbilityHolder).GetField("cooldowns", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) cooldowns = f.GetValue(selectedEntity.abilityHolder) as Dictionary<Ability, float>;
            }
            // Try get Player Cooldowns
            else if (selectedEntity.playerAbilityHolder != null)
            {
                FieldInfo f = typeof(PlayerAbilityHolder).GetField("cooldowns", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) cooldowns = f.GetValue(selectedEntity.playerAbilityHolder) as Dictionary<Ability, float>;
            }

            if (cooldowns != null)
            {
                foreach (var kvp in cooldowns)
                {
                    float remaining = kvp.Value - Time.time;
                    if (remaining > 0) cooldownString += $"{kvp.Key.abilityName}: <color=orange>{remaining:F1}s</color>\n";
                }
            }
            if (string.IsNullOrEmpty(cooldownString)) cooldownString = "<color=green>All Ready</color>";
            cooldownsText.text = cooldownString;
        }
    }

    private void InitializeGraph()
    {
        if (graphImage == null) return;
        graphTexture = new Texture2D(GRAPH_WIDTH, GRAPH_HEIGHT, TextureFormat.RGBA32, false);
        graphTexture.filterMode = FilterMode.Point;
        clearColors = new Color[GRAPH_WIDTH * GRAPH_HEIGHT];
        for (int i = 0; i < clearColors.Length; i++) clearColors[i] = graphBgColor;
        graphTexture.SetPixels(clearColors);
        graphTexture.Apply();
        graphImage.texture = graphTexture;
    }

    private void UpdateGraph(float currentMs)
    {
        if (graphImage == null) return;
        perfHistory.Add(currentMs);
        if (perfHistory.Count > GRAPH_WIDTH) perfHistory.RemoveAt(0);

        if (perfStatsText)
        {
            float avg = perfHistory.Count > 0 ? perfHistory.Average() : 0;
            float max = perfHistory.Count > 0 ? perfHistory.Max() : 0;
            string color = currentMs > 0.5f ? "red" : "green";
            perfStatsText.text = $"<color={color}>{currentMs:F2}ms</color> | Avg: {avg:F2} | Peak: {max:F2}";
        }

        graphTexture.SetPixels(clearColors);
        int thresholdY = Mathf.FloorToInt((0.5f / MAX_GRAPH_MS) * GRAPH_HEIGHT);
        for (int x = 0; x < GRAPH_WIDTH; x++) graphTexture.SetPixel(x, thresholdY, new Color(1f, 1f, 0f, 0.3f));

        for (int i = 0; i < perfHistory.Count; i++)
        {
            float val = perfHistory[i];
            int height = Mathf.Clamp(Mathf.FloorToInt((val / MAX_GRAPH_MS) * GRAPH_HEIGHT), 1, GRAPH_HEIGHT - 1);
            Color col = val > 0.5f ? graphSpikeColor : graphLineColor;
            for (int y = 0; y < height; y++) graphTexture.SetPixel(i, y, col);
        }
        graphTexture.Apply();
    }
}