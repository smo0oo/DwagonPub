using UnityEngine;
using TMPro;
using PixelCrushers.DialogueSystem;
using System.Reflection;
using System.Collections;

public class CustomAlertManager : MonoBehaviour
{
    public static CustomAlertManager instance;

    [Header("UI References")]
    [Tooltip("The Canvas Group of the panel you want to fade in and out.")]
    public CanvasGroup alertCanvasGroup;
    [Tooltip("The text element that will display the message.")]
    public TextMeshProUGUI alertText;

    [Header("Animation Settings")]
    public float fadeDuration = 0.5f;
    public float displayDuration = 3.0f;

    private Coroutine activeAlertCoroutine;

    void Awake()
    {
        // Standard Singleton Setup
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Ensure it starts invisible
        if (alertCanvasGroup != null)
        {
            alertCanvasGroup.alpha = 0f;
            alertCanvasGroup.interactable = false;
            alertCanvasGroup.blocksRaycasts = false;
        }
    }

    void OnEnable()
    {
        // AAA Standard: Use pure C# reflection to guarantee the method binds to Lua.
        MethodInfo method = typeof(CustomAlertManager).GetMethod("TriggerCustomAlert", new[] { typeof(string) });

        if (method != null)
        {
            // Inject a BRAND NEW command into the database. Pixel Crushers cannot block this.
            Lua.RegisterFunction("NotifyPlayer", this, method);
            Debug.Log("<color=green>[CustomAlertManager]</color> Successfully injected 'NotifyPlayer' into Lua.");
        }
        else
        {
            Debug.LogError("[CustomAlertManager] Failed to find method via Reflection.");
        }
    }

    void OnDisable()
    {
        Lua.UnregisterFunction("NotifyPlayer");
    }

    // This MUST be public so Reflection can find it
    public void TriggerCustomAlert(string message)
    {
        // Prove that the signal successfully reached the script!
        Debug.Log($"<color=cyan>[CustomAlertManager]</color> SUCCESS! Lua triggered NotifyPlayer with: {message}");

        if (activeAlertCoroutine != null)
        {
            StopCoroutine(activeAlertCoroutine);
        }
        activeAlertCoroutine = StartCoroutine(AlertRoutine(message));
    }

    private IEnumerator AlertRoutine(string message)
    {
        if (alertText != null) alertText.text = message;

        // PHASE 1: Fade In (Using Unscaled Time so it ignores paused game states)
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            if (alertCanvasGroup != null) alertCanvasGroup.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
            yield return null;
        }
        if (alertCanvasGroup != null) alertCanvasGroup.alpha = 1f;

        // PHASE 2: Hold on Screen
        yield return new WaitForSecondsRealtime(displayDuration);

        // PHASE 3: Fade Out
        t = 0;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            if (alertCanvasGroup != null) alertCanvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeDuration);
            yield return null;
        }
        if (alertCanvasGroup != null) alertCanvasGroup.alpha = 0f;

        activeAlertCoroutine = null;
    }
}