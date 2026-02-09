using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager instance;

    [Header("Data Asset")]
    public LoadingScreenData data;

    [Header("UI References")]
    public GameObject loadingScreenPanel;
    public CanvasGroup canvasGroup;
    public Image backgroundImage;
    public TextMeshProUGUI tipText;

    private ResourceRequest currentLoadRequest;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
    }

    public Coroutine ShowLoadingScreen(float duration)
    {
        return StartCoroutine(ShowRoutine(duration));
    }

    private IEnumerator ShowRoutine(float duration)
    {
        // 1. Prepare UI (Hidden)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f; // Ensure transparent before enabling
            canvasGroup.blocksRaycasts = true;
        }

        if (loadingScreenPanel != null)
        {
            loadingScreenPanel.SetActive(true);
        }

        // [FIX] Start Fade Immediately (Parallel to loading)
        // This ensures snappy response to user input
        Tween fadeTween = null;
        if (canvasGroup != null)
        {
            fadeTween = canvasGroup.DOFade(1f, duration);
        }

        // 2. Load Content (Background)
        if (data != null)
        {
            if (data.backgroundResourcesPaths.Count > 0)
            {
                string randomPath = data.backgroundResourcesPaths[Random.Range(0, data.backgroundResourcesPaths.Count)];

                // Load from Resources/LoadingScreens/
                currentLoadRequest = Resources.LoadAsync<Sprite>($"LoadingScreens/{randomPath}");

                // Allow the load to process while the fade happens
                yield return currentLoadRequest;

                if (currentLoadRequest.asset != null && backgroundImage != null)
                {
                    backgroundImage.sprite = currentLoadRequest.asset as Sprite;
                    backgroundImage.color = Color.white; // Show image natural color
                }
                else
                {
                    // Fallback if load fails
                    if (backgroundImage != null) backgroundImage.color = Color.black;
                }
            }
            else
            {
                // No paths in list -> Fallback to black
                if (backgroundImage != null) backgroundImage.color = Color.black;
            }

            if (data.loadingTips.Count > 0 && tipText != null)
            {
                tipText.text = data.loadingTips[Random.Range(0, data.loadingTips.Count)];
            }
        }

        // 3. Wait for Fade to Complete
        // If resource load was faster than fade, wait for the rest of the duration.
        // If load was slower, we proceed immediately (fade is already done).
        if (fadeTween != null && fadeTween.IsActive())
        {
            yield return fadeTween.WaitForCompletion();
        }
        else if (canvasGroup == null)
        {
            yield return new WaitForSeconds(duration);
        }
    }

    public void HideLoadingScreen(float duration)
    {
        if (canvasGroup == null)
        {
            FinishHide();
            return;
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.DOFade(0f, duration).OnComplete(FinishHide);
    }

    private void FinishHide()
    {
        if (loadingScreenPanel != null)
        {
            loadingScreenPanel.SetActive(false);
        }

        // --- CLEANUP MEMORY ---
        if (backgroundImage != null) backgroundImage.sprite = null;
        currentLoadRequest = null;
        Resources.UnloadUnusedAssets();
    }
}