// PlayerPortraitUI.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class PlayerPortraitUI
{
    [Tooltip("The parent object for this portrait, which will be scaled.")]
    public GameObject portraitFrame;

    // --- NEW: Safe Hiding Reference ---
    [Tooltip("The Canvas Group on the portrait frame, used to hide it safely without disabling the GameObject.")]
    public CanvasGroup canvasGroup;

    [Tooltip("The UI Image for the character's portrait.")]
    public Image portraitImage;
    [Tooltip("The TextMeshProUGUI for the character's name.")]
    public TextMeshProUGUI nameText;
    [Tooltip("The TextMeshProUGUI for the character's class.")]
    public TextMeshProUGUI classText;
    [Tooltip("The Slider for the character's health.")]
    public Slider hpSlider;

    [Tooltip("The parent object where status effect icons will be created.")]
    public Transform statusEffectContainer;

    [Tooltip("The TextMeshPro text element for displaying the AI's current status (e.g., Following, Attacking).")]
    public TextMeshProUGUI statusText;

    [Tooltip("The Button component for changing the AI stance.")]
    public Button stanceButton;
    [Tooltip("The Image component of the stance button, used to change the icon.")]
    public Image stanceIconImage;
}