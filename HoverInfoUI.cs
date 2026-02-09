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

    [Header("Health Display")]
    [Tooltip("The Image component for the health bar fill.")]
    public Image healthBarFill;
    [Tooltip("The TextMeshProUGUI component to display HP numbers (e.g. '150 / 200').")]
    public TextMeshProUGUI healthText; // [ADDED]

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
    private StatusEffectHolder currentStatusHolder;

    void Start()
    {
        mainCamera = Camera.main;
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        if (statusEffectsContainer != null)
        {
            foreach (Transform child in statusEffectsContainer) Destroy(child.gameObject);
        }
    }

    void Update()
    {
        // 1. Mouse over UI check
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
                if (hitHealth != currentTargetHealth)
                {
                    ShowInfo(hitHealth);
                }

                // Update dynamic bars continuously
                UpdateHealthBar();
            }
            else
            {
                if (currentTargetHealth) HideInfo();
            }
        }
        else
        {
            if (currentTargetHealth) HideInfo();
        }
    }

    private void ShowInfo(Health target)
    {
        if (currentTargetHealth) HideInfo();

        currentTargetHealth = target;

        // --- 1. SETUP NAME & ICON ---
        DialogueActor actor = currentTargetHealth.GetComponentInParent<DialogueActor>();
        if (actor != null)
        {
            var dbActor = DialogueManager.MasterDatabase.GetActor(actor.actor);
            if (dbActor != null)
            {
                string displayName = dbActor.LookupValue("Display Name");
                nameText.text = !string.IsNullOrEmpty(displayName) ? displayName : dbActor.Name;

                if (dbActor.portrait != null && iconImage != null)
                {
                    Texture2D portraitTex = dbActor.portrait;
                    iconImage.sprite = Sprite.Create(portraitTex, new Rect(0, 0, portraitTex.width, portraitTex.height), new Vector2(0.5f, 0.5f));
                    iconImage.enabled = true;
                }
                else if (iconImage != null)
                {
                    iconImage.enabled = false;
                }
            }
        }
        else
        {
            if (nameText != null) nameText.text = currentTargetHealth.gameObject.name;
            if (iconImage != null) iconImage.enabled = false;
        }

        // --- 2. SETUP STATUS EFFECTS ---
        currentStatusHolder = currentTargetHealth.GetComponentInParent<StatusEffectHolder>();
        if (currentStatusHolder != null)
        {
            currentStatusHolder.OnEffectsChanged += RefreshStatusIcons;
            RefreshStatusIcons(currentStatusHolder);
        }

        UpdateHealthBar();
        infoPanel.SetActive(true);
    }

    private void HideInfo()
    {
        if (currentStatusHolder != null)
        {
            currentStatusHolder.OnEffectsChanged -= RefreshStatusIcons;
            currentStatusHolder = null;
        }

        currentTargetHealth = null;
        infoPanel.SetActive(false);
    }

    private void RefreshStatusIcons(StatusEffectHolder holder)
    {
        if (statusEffectsContainer == null || statusIconPrefab == null) return;

        foreach (Transform child in statusEffectsContainer)
        {
            Destroy(child.gameObject);
        }

        List<ActiveStatusEffect> activeEffects = holder.GetActiveEffects();

        foreach (var effect in activeEffects)
        {
            if (effect.EffectData != null && effect.EffectData.icon != null)
            {
                GameObject newIconObj = Instantiate(statusIconPrefab, statusEffectsContainer);
                Image iconImg = newIconObj.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.sprite = effect.EffectData.icon;
                }
            }
        }
    }

    private void UpdateHealthBar()
    {
        if (currentTargetHealth == null) return;

        // [ADDED] Update Fill
        if (healthBarFill != null)
        {
            if (currentTargetHealth.maxHealth > 0)
                healthBarFill.fillAmount = (float)currentTargetHealth.currentHealth / currentTargetHealth.maxHealth;
            else
                healthBarFill.fillAmount = 0;
        }

        // [ADDED] Update Text (e.g. "150 / 200")
        if (healthText != null)
        {
            healthText.text = $"{currentTargetHealth.currentHealth} / {currentTargetHealth.maxHealth}";
        }
    }

    void OnDisable()
    {
        HideInfo();
    }
}