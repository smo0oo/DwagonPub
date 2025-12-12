using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("Health UI References")]
    public Image healthBarFill;

    [Header("Casting UI References")]
    public GameObject castBarPanel;
    public Image castBarFill;
    public TextMeshProUGUI castNameText;

    // --- NEW UI REFERENCE ---
    [Header("Status Display")]
    [Tooltip("The TextMeshPro text element used to display the AI's current state and action.")]
    public TextMeshProUGUI statusText;
    // --- END OF NEW ---

    private Health targetHealth;
    private Transform cameraTransform;
    private Coroutine activeCastCoroutine;

    void Awake()
    {
        targetHealth = GetComponentInParent<Health>();
        if (targetHealth == null)
        {
            gameObject.SetActive(false);
            return;
        }
        targetHealth.OnHealthChanged += UpdateHealthBar;
    }

    void Start()
    {
        cameraTransform = Camera.main.transform;
        UpdateHealthBar();
        if (castBarPanel != null) castBarPanel.SetActive(false);
        // Initialize the status text as empty
        if (statusText != null) statusText.text = "";
    }

    void OnDestroy()
    {
        if (targetHealth != null) targetHealth.OnHealthChanged -= UpdateHealthBar;
    }

    void LateUpdate()
    {
        if (cameraTransform != null)
        {
            transform.LookAt(transform.position + cameraTransform.rotation * Vector3.forward,
                             cameraTransform.rotation * Vector3.up);
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = (float)targetHealth.currentHealth / targetHealth.maxHealth;
        }
    }

    // --- NEW PUBLIC METHOD ---
    /// <summary>
    /// Updates the status text on the enemy's UI.
    /// </summary>
    /// <param name="state">The current AI state (e.g., Idle, Combat).</param>
    /// <param name="action">The current action (e.g., Chasing, Attacking).</param>
    public void UpdateStatus(string state, string action)
    {
        if (statusText != null)
        {
            statusText.text = $"{state} :: {action}";
        }
    }
    // --- END OF NEW ---

    public void StartCast(string abilityName, float castDuration)
    {
        if (castBarPanel == null) return;
        if (activeCastCoroutine != null) StopCoroutine(activeCastCoroutine);
        if (castNameText != null) castNameText.text = abilityName;
        castBarPanel.SetActive(true);
        activeCastCoroutine = StartCoroutine(AnimateCastBar(castDuration));
    }

    public void StopCast()
    {
        if (castBarPanel == null) return;
        if (activeCastCoroutine != null)
        {
            StopCoroutine(activeCastCoroutine);
            activeCastCoroutine = null;
        }
        castBarPanel.SetActive(false);
    }

    private IEnumerator AnimateCastBar(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (castBarFill != null)
            {
                castBarFill.fillAmount = timer / duration;
            }
            timer += Time.deltaTime;
            yield return null;
        }
        StopCast();
    }
}