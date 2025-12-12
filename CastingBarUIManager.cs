using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CastingBarUIManager : MonoBehaviour
{
    public static CastingBarUIManager instance;

    [Header("UI References")]
    public GameObject castBarPanel;
    // --- MODIFIED: Changed from Slider to Image ---
    [Tooltip("The Image component for the cast bar. Its Image Type must be set to 'Filled'.")]
    public Image castFillImage;
    public TextMeshProUGUI abilityNameText;

    private Coroutine activeCastCoroutine;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    void Start()
    {
        if (castBarPanel != null)
        {
            castBarPanel.SetActive(false);
        }
    }

    public void StartCast(string abilityName, float castDuration)
    {
        if (castBarPanel == null) return;

        if (activeCastCoroutine != null)
        {
            StopCoroutine(activeCastCoroutine);
        }

        abilityNameText.text = abilityName;
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
            // --- MODIFIED: Changed from .value to .fillAmount ---
            castFillImage.fillAmount = timer / duration;
            timer += Time.deltaTime;
            yield return null;
        }
        // --- MODIFIED: Changed from .value to .fillAmount ---
        castFillImage.fillAmount = 1f;

        yield return new WaitForSeconds(0.2f);
        castBarPanel.SetActive(false);
        activeCastCoroutine = null;
    }
}
