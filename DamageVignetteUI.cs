using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageVignetteUI : MonoBehaviour
{
    [Header("UI References")]
    public Image vignetteImage;

    [Header("Flash Settings")]
    public Color flashColor = new Color(1f, 0f, 0f, 0.4f);
    public float fadeOutDuration = 0.4f;

    private Coroutine flashCoroutine;

    void Awake()
    {
        if (vignetteImage != null)
        {
            vignetteImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            vignetteImage.raycastTarget = false;
        }
    }

    void OnEnable()
    {
        Health.OnDamageTaken += HandleGlobalDamage;
    }

    void OnDisable()
    {
        Health.OnDamageTaken -= HandleGlobalDamage;
    }

    private void HandleGlobalDamage(DamageInfo info)
    {
        if (vignetteImage == null || PartyManager.instance == null || info.Target == null) return;

        GameObject activePlayer = PartyManager.instance.ActivePlayer;
        if (activePlayer == null) return;

        // FIXED: Check if the target IS the player, OR if the target is a CHILD of the player (like a hitbox/model)
        bool isActivePlayerHit = (info.Target == activePlayer) || info.Target.transform.IsChildOf(activePlayer.transform);

        if (isActivePlayerHit)
        {
            TriggerFlash();
        }
    }

    // NEW: Right-click the script in the Inspector to test the UI manually!
    [ContextMenu("Test Vignette Flash")]
    public void TriggerFlash()
    {
        if (vignetteImage == null) return;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        vignetteImage.color = flashColor;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;

            float currentAlpha = Mathf.Lerp(flashColor.a, 0f, t);
            vignetteImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, currentAlpha);

            yield return null;
        }

        vignetteImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
    }
}