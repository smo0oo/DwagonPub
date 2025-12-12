using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WagonHotbarManager : MonoBehaviour
{
    [Header("UI References")]
    public List<WagonHotbarSlot> wagonHotbarSlots;

    [Header("Keybinds")]
    [Tooltip("The keys used to trigger the wagon abilities. Should match the number of slots.")]
    public KeyCode[] keyCodes = { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4 };

    // --- MODIFIED: This is now the correct DomeAbilityHolder ---
    private DomeAbilityHolder casterAbilityHolder;

    void Start()
    {
        // The GameManager is responsible for showing/hiding this UI.
    }

    void Update()
    {
        if (casterAbilityHolder == null) return;

        for (int i = 0; i < keyCodes.Length; i++)
        {
            if (i < wagonHotbarSlots.Count && Input.GetKeyDown(keyCodes[i]))
            {
                wagonHotbarSlots[i].SendMessage("OnPointerClick", new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    // ### THIS METHOD HAS BEEN MODIFIED ###
    public void InitializeAndShow()
    {
        // --- FIX: Replaced FindObjectOfType with FindAnyObjectByType ---
        DomeController dome = FindAnyObjectByType<DomeController>();
        if (dome == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // --- MODIFIED: Gets the DomeAbilityHolder now ---
        casterAbilityHolder = dome.GetComponentInChildren<DomeAbilityHolder>();
        DomeAI domeAI = dome.GetComponentInChildren<DomeAI>();

        if (casterAbilityHolder == null || domeAI == null)
        {
            Debug.LogError("WagonHotbarManager: The instantiated Dome is missing a DomeAbilityHolder or DomeAI component.", dome.gameObject);
            gameObject.SetActive(false);
            return;
        }

        var wagonAbilities = domeAI.defaultAbilities
            .Where(a => a.usageType == AIUsageType.WagonCallable)
            .ToList();

        for (int i = 0; i < wagonHotbarSlots.Count; i++)
        {
            if (i < wagonAbilities.Count)
            {
                string keybind = i < keyCodes.Length ? keyCodes[i].ToString() : "";
                wagonHotbarSlots[i].Initialize(this, wagonAbilities[i], keybind);
                wagonHotbarSlots[i].gameObject.SetActive(true);
            }
            else
            {
                wagonHotbarSlots[i].gameObject.SetActive(false);
            }
        }
    }

    // ### THIS METHOD HAS BEEN MODIFIED ###
    public void TriggerAbility(Ability ability)
    {
        if (casterAbilityHolder == null || !casterAbilityHolder.CanUseAbility(ability, casterAbilityHolder.gameObject)) return;

        switch (ability.abilityType)
        {
            case AbilityType.GroundAOE:
            case AbilityType.GroundPlacement:
                if (TargetingController.instance != null)
                {
                    // Now correctly passes the DomeAbilityHolder to the generic StartTargeting method
                    TargetingController.instance.StartTargeting(ability, casterAbilityHolder);
                }
                break;

            case AbilityType.Self:
            default:
                casterAbilityHolder.UseAbility(ability, casterAbilityHolder.gameObject);
                break;
        }
    }
}