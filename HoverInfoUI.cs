using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCrushers.DialogueSystem; // Required for Dialogue Manager integration

/// <summary>
/// Manages a UI panel that displays information (name, icon, health) for a character 
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

    [Header("Raycasting Settings")]
    [Tooltip("The layers that this script should detect for showing info (e.g., Enemy, Friendly).")]
    public LayerMask hoverableLayers;

    private Camera mainCamera;
    private Health currentTargetHealth;

    void Start()
    {
        mainCamera = Camera.main;
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (currentTargetHealth)
            {
                HideInfo();
            }
            return;
        }

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

                // --- FIX: Update health bar every frame you are hovering ---
                // This replaces the OnHealthChanged subscription
                UpdateHealthBar();
            }
            else
            {
                if (currentTargetHealth)
                {
                    HideInfo();
                }
            }
        }
        else
        {
            if (currentTargetHealth)
            {
                HideInfo();
            }
        }
    }

    /// <summary>
    /// Shows the UI panel and populates it with data from the target character.
    /// </summary>
    private void ShowInfo(Health target)
    {
        if (currentTargetHealth)
        {
            HideInfo();
        }

        currentTargetHealth = target;
        // --- FIX: Event subscription removed ---
        // currentTargetHealth.OnHealthChanged += UpdateHealthBar;

        // Get the DialogueActor component from the character we hit.
        DialogueActor actor = currentTargetHealth.GetComponentInParent<DialogueActor>();
        if (actor != null)
        {
            // Use the actor's ID string to look up their full record in the database.
            Actor dbActor = DialogueManager.MasterDatabase.GetActor(actor.actor);
            if (dbActor != null)
            {
                // Get the "Display Name" field from the database.
                string displayName = dbActor.LookupValue("Display Name");
                if (!string.IsNullOrEmpty(displayName))
                {
                    nameText.text = displayName;
                }
                else
                {
                    nameText.text = dbActor.Name; // Fallback to the internal actor name
                }

                // --- CLARIFIED ICON LOGIC ---
                // The database stores the portrait as a Texture2D.
                if (dbActor.portrait != null)
                {
                    // We create a Sprite from the Texture2D at runtime to display it.
                    Texture2D portraitTex = dbActor.portrait;
                    iconImage.sprite = Sprite.Create(portraitTex, new Rect(0, 0, portraitTex.width, portraitTex.height), new Vector2(0.5f, 0.5f));
                    iconImage.enabled = true;
                }
                else
                {
                    // If no portrait is in the database, hide the icon.
                    iconImage.enabled = false;
                }
            }
        }
        else
        {
            // If the character has no DialogueActor, fall back to the GameObject's name and hide the icon.
            nameText.text = currentTargetHealth.gameObject.name;
            iconImage.enabled = false;
        }

        UpdateHealthBar();
        infoPanel.SetActive(true);
    }

    /// <summary>
    /// Hides the UI panel and clears the current target.
    /// </summary>
    private void HideInfo()
    {
        // --- FIX: Event unsubscription removed ---
        // if (currentTargetHealth)
        // {
        //     currentTargetHealth.OnHealthChanged -= UpdateHealthBar;
        // }

        currentTargetHealth = null;
        infoPanel.SetActive(false);
    }

    /// <summary>
    /// Updates the health bar fill amount based on the current target's health.
    /// </summary>
    private void UpdateHealthBar()
    {
        if (currentTargetHealth && healthBarFill != null)
        {
            if (currentTargetHealth.maxHealth > 0)
            {
                healthBarFill.fillAmount = (float)currentTargetHealth.currentHealth / currentTargetHealth.maxHealth;
            }
            else
            {
                healthBarFill.fillAmount = 0;
            }
        }
    }

    void OnDisable()
    {
        // --- FIX: Event unsubscription removed ---
        // if (currentTargetHealth)
        // {
        //     currentTargetHealth.OnHealthChanged -= UpdateHealthBar;
        // }
    }
}