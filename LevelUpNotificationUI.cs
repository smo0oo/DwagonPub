using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class LevelUpNotificationUI : MonoBehaviour
{
    [Header("UI Structure")]
    [Tooltip("The main panel/root object to turn On/Off.")]
    public GameObject notificationPanel;

    [Header("Text References")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI subText; // e.g. "Rewards Unlocked!"

    [Header("Audio")]
    public AudioClip levelUpSound;
    public AudioSource uiAudioSource;

    [Header("Animation Settings")]
    public float fadeInDuration = 0.5f;
    public float displayDuration = 2.0f;
    public float fadeOutDuration = 0.5f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        // Ensure everything starts hidden and deactivated
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
        if (notificationPanel != null) notificationPanel.SetActive(false);
    }

    void Start()
    {
        if (PartyManager.instance != null)
        {
            PartyManager.instance.OnLevelUp += ShowNotification;
        }
    }

    void OnDestroy()
    {
        if (PartyManager.instance != null)
        {
            PartyManager.instance.OnLevelUp -= ShowNotification;
        }
    }

    private void ShowNotification()
    {
        // 1. Activate the Panel
        if (notificationPanel != null) notificationPanel.SetActive(true);

        // 2. Set Text
        int newLevel = PartyManager.instance.partyLevel;
        if (levelText != null) levelText.text = $"Level {newLevel}!";
        //if (subText != null) subText.text = $"Everyone gained +{PartyManager.instance.pointsPerLevel} Stat Points!";

        // 3. Play Sound
        if (uiAudioSource != null && levelUpSound != null)
        {
            uiAudioSource.PlayOneShot(levelUpSound);
        }

        // 4. Run Animation
        StopAllCoroutines();
        StartCoroutine(AnimatePopup());
    }

    private IEnumerator AnimatePopup()
    {
        // Fade In
        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Wait
        yield return new WaitForSeconds(displayDuration);

        // Fade Out
        timer = 0f;
        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        // Turn off the panel completely
        if (notificationPanel != null) notificationPanel.SetActive(false);
    }
}