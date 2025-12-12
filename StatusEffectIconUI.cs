using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectIconUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI durationText;
    public Image borderImage; // Optional, for buff/debuff color

    private ActiveStatusEffect effectToTrack;

    public void Initialize(ActiveStatusEffect effect)
    {
        effectToTrack = effect;
        iconImage.sprite = effect.EffectData.icon;

        // Optional: Color the border based on buff/debuff
        if (borderImage != null)
        {
            borderImage.color = effect.EffectData.isBuff ? Color.green : Color.red;
        }
    }

    void Update()
    {
        if (effectToTrack == null || effectToTrack.IsFinished)
        {
            // The manager will destroy this object, but this is a safe fallback.
            Destroy(gameObject);
            return;
        }

        // Update the duration text if the effect is timed
        if (effectToTrack.EffectData.durationType == DurationType.Timed)
        {
            if (effectToTrack.RemainingDuration < 5)
            {
                durationText.text = effectToTrack.RemainingDuration.ToString("F1"); // Show one decimal place
            }
            else
            {
                durationText.text = Mathf.CeilToInt(effectToTrack.RemainingDuration).ToString();
            }
        }
        else
        {
            durationText.text = ""; // No text for Infinite effects
        }
    }
}