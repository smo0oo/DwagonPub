using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCrushers.DialogueSystem;
using System.Collections.Generic;

/// <summary>
/// Manages a UI panel that displays information (name, icon, health, status effects) for a character 
/// the player is currently hovering their mouse over in the game world.
/// </summary>
public class HoverInfoUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent GameObject of the entire hover info panel.")]
    public GameObject infoPanel;
    [Tooltip("The Image component for the character's icon.")]
    public Image iconImage;
    [Tooltip("The TextMeshProUGUI component for the character's name.")]
    public TextMeshProUGUI nameText;
    [Tooltip("The Image component for the health bar fill.")]
    public Image healthBarFill;

    [Header("Status Effects (AAA Feature)")]
    [Tooltip("Assign a GameObject with a HorizontalLayoutGroup to hold the icons.")]
    public Transform statusEffectsContainer;
    [Tooltip("A simple prefab containing an Image component to display the buff/debuff icon.")]
    public GameObject statusIconPrefab;

    [Header("Raycasting Settings")]
    [Tooltip("The layers that this script should detect for showing info (e.g., Enemy, Friendly).")]
    public LayerMask hoverableLayers;

    private Camera mainCamera;
    private Health currentTargetHealth;
    private StatusEffectHolder currentStatusHolder; // Reference to the target's status system

    void Start()
    {
        mainCamera = Camera.main;
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        // Hide the container logic if not assigned
        if (statusEffectsContainer != null)
        {
            // Clear any dummy icons from editor
            foreach (Transform child in statusEffectsContainer) Destroy(child.gameObject);
        }
    }

    void Update()
    {
        // 1. Mouse over UI check (prevents clicking through interfaces)
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (currentTargetHealth) HideInfo();
            return;
        }

        // 2. Raycast for entities
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, hoverableLayers, QueryTriggerInteraction.Collide))
        {
            Health hitHealth = hit.collider.GetComponentInParent<Health>();

            if (hitHealth != null)
            {
                // New target detected?
                if (hitHealth != currentTargetHealth)
                {
                    ShowInfo(hitHealth);
                }

                // Update dynamic bars (Health is continuous, so we update it here)
                UpdateHealthBar();
            }
            else
            {
                // Hit something, but it wasn't an entity (e.g. wall)
                if (currentTargetHealth) HideInfo();
            }
        }
        else
        {
            // Mouse hitting nothing (skybox)
            if (currentTargetHealth) HideInfo();
        }
    }

    /// <summary>
    /// Shows the UI panel and populates it with data from the target character.
    /// </summary>
    private void ShowInfo(Health target)
    {
        // Clean up previous target if switching directly from one enemy to another
        if (currentTargetHealth) HideInfo();

        currentTargetHealth = target;

        // --- 1. SETUP NAME & ICON (Dialogue System) ---
        DialogueActor actor = currentTargetHealth.GetComponentInParent<DialogueActor>();
        if (actor != null)
        {
            var dbActor = DialogueManager.MasterDatabase.GetActor(actor.actor);
            if (dbActor != null)
            {
                string displayName = dbActor.LookupValue("Display Name");
                nameText.text = !string.IsNullOrEmpty(displayName) ? displayName : dbActor.Name;

                if (dbActor.portrait != null)
                {
                    Texture2D portraitTex = dbActor.portrait;
                    iconImage.sprite = Sprite.Create(portraitTex, new Rect(0, 0, portraitTex.width, portraitTex.height), new Vector2(0.5f, 0.5f));
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }
        }
        else
        {
            nameText.text = currentTargetHealth.gameObject.name;
            iconImage.enabled = false;
        }

        // --- 2. SETUP STATUS EFFECTS ---
        // Find the StatusEffectHolder on the target
        currentStatusHolder = currentTargetHealth.GetComponentInParent<StatusEffectHolder>();
        if (currentStatusHolder != null)
        {
            // Subscribe to changes so we see buffs applied in real-time
            currentStatusHolder.OnEffectsChanged += RefreshStatusIcons;

            // Initial draw
            RefreshStatusIcons(currentStatusHolder);
        }

        UpdateHealthBar();
        infoPanel.SetActive(true);
    }

    /// <summary>
    /// Hides the UI panel and clears the current target.
    /// </summary>
    private void HideInfo()
    {
        // Unsubscribe to prevent memory leaks or errors when target dies/disappears
        if (currentStatusHolder != null)
        {
            currentStatusHolder.OnEffectsChanged -= RefreshStatusIcons;
            currentStatusHolder = null;
        }

        currentTargetHealth = null;
        infoPanel.SetActive(false);
    }

    /// <summary>
    /// Rebuilds the icon list. Called whenever the target's status effects change.
    /// </summary>
    private void RefreshStatusIcons(StatusEffectHolder holder)
    {
        if (statusEffectsContainer == null || statusIconPrefab == null) return;

        // 1. Clear existing icons
        foreach (Transform child in statusEffectsContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Get active effects
        List<ActiveStatusEffect> activeEffects = holder.GetActiveEffects();

        // 3. Spawn new icons
        foreach (var effect in activeEffects)
        {
            if (effect.EffectData != null && effect.EffectData.icon != null)
            {
                GameObject newIconObj = Instantiate(statusIconPrefab, statusEffectsContainer);

                // Set the Icon Sprite
                Image iconImg = newIconObj.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.sprite = effect.EffectData.icon;
                }

                // Optional: You could tint the icon based on IsBuff
                // if (!effect.EffectData.isBuff) iconImg.color = Color.red; 
            }
        }
    }

    private void UpdateHealthBar()
    {
        if (currentTargetHealth && healthBarFill != null)
        {
            if (currentTargetHealth.maxHealth > 0)
                healthBarFill.fillAmount = (float)currentTargetHealth.currentHealth / currentTargetHealth.maxHealth;
            else
                healthBarFill.fillAmount = 0;
        }
    }

    void OnDisable()
    {
        HideInfo();
    }
}