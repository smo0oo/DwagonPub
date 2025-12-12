using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeOfDayUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The 'RawImage' component that has the scrolling sun and moon texture.")]
    public RawImage scrollingImage;

    [Tooltip("The TextMeshPro text element to display the current time numerically.")]
    public TextMeshProUGUI timeText;

    // --- FIX: Track total minutes to catch Hour changes too ---
    private int _lastTotalMinutes = -1;

    void Update()
    {
        if (WorldMapManager.instance == null) return;

        float currentTime = WorldMapManager.instance.timeOfDay;

        // --- Update the Text ---
        int hours = Mathf.FloorToInt(currentTime);
        int minutes = Mathf.FloorToInt((currentTime - hours) * 60);
        int totalMinutes = (hours * 60) + minutes;

        // --- FIX: Compare against total time ---
        if (timeText != null && totalMinutes != _lastTotalMinutes)
        {
            timeText.text = $"{hours:00}:{minutes:00}";
            _lastTotalMinutes = totalMinutes;
        }

        // --- Update the Scrolling Image ---
        if (scrollingImage != null)
        {
            float timePercentage = currentTime / 24f;
            Rect currentUVRect = scrollingImage.uvRect;
            scrollingImage.uvRect = new Rect(timePercentage, currentUVRect.y, currentUVRect.width, currentUVRect.height);
        }
    }
}