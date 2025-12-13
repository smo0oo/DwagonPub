using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening; // Make sure you have DOTween imported

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager instance;

    [Header("Data Asset")]
    [Tooltip("Assign your 'LoadingScreenData' ScriptableObject here.")]
    public LoadingScreenData data;

    [Header("UI References")]
    public GameObject loadingScreenPanel;
    public CanvasGroup canvasGroup;
    public Image backgroundImage;
    public TextMeshProUGUI tipText;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        // This object should persist across all scenes
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Start with the loading screen hidden
        if (loadingScreenPanel != null)
        {
            loadingScreenPanel.SetActive(false);
        }
    }

    public Coroutine ShowLoadingScreen(float duration)
    {
        return StartCoroutine(ShowRoutine(duration));
    }

    private IEnumerator ShowRoutine(float duration)
    {
        // Select random content
        if (data != null)
        {
            if (data.backgroundImages.Count > 0)
            {
                backgroundImage.sprite = data.backgroundImages[Random.Range(0, data.backgroundImages.Count)];
            }
            if (data.loadingTips.Count > 0)
            {
                tipText.text = data.loadingTips[Random.Range(0, data.loadingTips.Count)];
            }
        }

        // Activate and fade in
        if (loadingScreenPanel != null)
        {
            loadingScreenPanel.SetActive(true);
        }

        if (canvasGroup != null)
        {
            // --- FIX: Ensure we block raycasts again when showing ---
            canvasGroup.blocksRaycasts = true;
            // -------------------------------------------------------

            canvasGroup.alpha = 0f;
            yield return canvasGroup.DOFade(1f, duration).WaitForCompletion();
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
    }

    public void HideLoadingScreen(float duration)
    {
        if (canvasGroup == null)
        {
            if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
            return;
        }

        // --- FIX: Block raycasts immediately so players can click buttons while it fades ---
        canvasGroup.blocksRaycasts = false;
        // ---------------------------------------------------------------------------------

        // Fade out and deactivate
        canvasGroup.DOFade(0f, duration).OnComplete(() => {
            if (loadingScreenPanel != null)
            {
                loadingScreenPanel.SetActive(false);
            }
        });
    }
}