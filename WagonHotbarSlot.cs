using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class WagonHotbarSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI keybindText;

    private Ability assignedAbility;
    private WagonHotbarManager manager;

    public void Initialize(WagonHotbarManager owner, Ability ability, string keybind)
    {
        manager = owner;
        assignedAbility = ability;

        if (iconImage != null)
        {
            iconImage.sprite = ability.icon;
            iconImage.enabled = true;
        }
        if (keybindText != null)
        {
            keybindText.text = keybind;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null && assignedAbility != null && eventData.button == PointerEventData.InputButton.Left)
        {
            manager.TriggerAbility(assignedAbility);
        }
    }
}