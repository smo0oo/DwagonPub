using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

[RequireComponent(typeof(Canvas))] // Ensure we have a Canvas to sort
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager instance;

    [Header("Database")]
    public LoadingScreenData database;

    [Header("UI References")]
    public GameObject loadingScreenPanel;
    public CanvasGroup canvasGroup;
    public Image backgroundImage;

    [Header("Lore UI")]
    public TextMeshProUGUI loreTitleText;
    public TextMeshProUGUI loreDescriptionText;

    private Canvas _myCanvas;
    private string _targetSceneName;
    private SceneType _targetSceneType;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // --- AAA FIX: FORCE SORT ORDER ---
        _myCanvas = GetComponent<Canvas>();
        if (_myCanvas != null)
        {
            // 1. Force 'Screen Space - Overlay' to ensure it covers everything
            _myCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 2. Set to Max Value (32767 is max for 16-bit, but 9999 is usually safe/enough)
            // Standard UI is usually 0-100. Tooltips are ~1000. Loading Screen is 9999.
            _myCanvas.sortingOrder = 9999;
        }
        else
        {
            Debug.LogError("LoadingScreenManager: No Canvas component found! Please attach this script to your Root Canvas.");
        }
    }

    void Start()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
    }

    public void SetLoadContext(string nextSceneName, SceneType nextSceneType)
    {
        _targetSceneName = nextSceneName;
        _targetSceneType = nextSceneType;
    }

    public Coroutine ShowLoadingScreen(float duration)
    {
        return StartCoroutine(ShowRoutine(duration));
    }

    private IEnumerator ShowRoutine(float duration)
    {
        // 1. Prepare UI
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
        }
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);

        // Refresh Sort Order just in case something else changed it
        if (_myCanvas != null) _myCanvas.sortingOrder = 9999;

        // 2. Select Lore Content
        SetupLoreContent();

        // 3. Fade In (Force Update to ignore TimeScale, just in case game is paused)
        Tween fadeTween = null;
        if (canvasGroup != null)
        {
            fadeTween = canvasGroup.DOFade(1f, duration).SetUpdate(true);
        }

        if (fadeTween != null) yield return fadeTween.WaitForCompletion();
        else yield return new WaitForSecondsRealtime(duration);
    }

    private void SetupLoreContent()
    {
        if (database == null || database.entries.Count == 0) return;

        int currentLevel = (PartyManager.instance != null) ? PartyManager.instance.partyLevel : 1;

        var validEntries = database.entries.Where(e =>
            currentLevel >= e.minLevel &&
            currentLevel <= e.maxLevel &&
            (!e.requireSceneType || e.requiredSceneType == _targetSceneType)
        ).ToList();

        if (validEntries.Count == 0) validEntries = database.entries;

        LoreScreenEntry selected = validEntries[Random.Range(0, validEntries.Count)];

        if (backgroundImage != null)
        {
            if (selected.backgroundImage != null) backgroundImage.sprite = selected.backgroundImage;
            else backgroundImage.sprite = database.defaultFallbackImage;
            backgroundImage.color = Color.white;
        }

        if (loreTitleText != null) loreTitleText.text = selected.title;
        if (loreDescriptionText != null) loreDescriptionText.text = selected.description;
    }

    public void HideLoadingScreen(float duration)
    {
        if (canvasGroup == null) { FinishHide(); return; }

        canvasGroup.blocksRaycasts = false;

        // Use SetUpdate(true) to fade out even if game is paused
        canvasGroup.DOFade(0f, duration).SetUpdate(true).OnComplete(FinishHide);
    }

    private void FinishHide()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
    }
}