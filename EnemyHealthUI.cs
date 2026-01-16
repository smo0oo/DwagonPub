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

    [Header("Status Display")]
    public TextMeshProUGUI statusText;

    private Health targetHealth;
    private Transform cameraTransform;
    private Coroutine activeCastCoroutine;
    private Transform _transform; // Cached transform

    void Awake()
    {
        _transform = transform;
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
        if (Camera.main != null) cameraTransform = Camera.main.transform;

        UpdateHealthBar();
        if (castBarPanel != null) castBarPanel.SetActive(false);
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
            // --- OPTIMIZATION: Direct Rotation Copy ---
            // Much faster than LookAt() with Vector math.
            // This aligns the UI plane perfectly with the camera plane.
            _transform.rotation = cameraTransform.rotation;
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = (float)targetHealth.currentHealth / targetHealth.maxHealth;
        }
    }

    public void UpdateStatus(string state, string action)
    {
        if (statusText != null)
        {
            // Note: String interpolation still creates some garbage, 
            // but since EnemyAI filters redundant calls, this is acceptable.
            statusText.text = $"{state} :: {action}";
        }
    }

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