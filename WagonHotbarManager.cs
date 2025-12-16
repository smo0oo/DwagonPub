using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class WagonHotbarManager : MonoBehaviour
{
    [Header("UI References")]
    public List<WagonHotbarSlot> wagonHotbarSlots;

    [Header("Keybinds")]
    [Tooltip("The keys used to trigger the wagon abilities. Should match the number of slots.")]
    public KeyCode[] keyCodes = { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4 };

    private DomeAbilityHolder casterAbilityHolder;

    // Wait loop to ensure we hook up even if the Dome loads late
    IEnumerator Start()
    {
        while (DomeController.instance == null)
        {
            yield return null;
        }
        // Small buffer to ensure DomeAI is initialized
        yield return null;

        InitializeAndShow();
    }

    void Update()
    {
        if (casterAbilityHolder == null) return;

        for (int i = 0; i < keyCodes.Length; i++)
        {
            if (i < wagonHotbarSlots.Count && Input.GetKeyDown(keyCodes[i]))
            {
                if (wagonHotbarSlots[i].gameObject.activeSelf)
                {
                    var pointerData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                    wagonHotbarSlots[i].OnPointerClick(pointerData);
                }
            }
        }
    }

    public void InitializeAndShow()
    {
        DomeController dome = DomeController.instance;

        // If Dome is not ready yet, just return. Do NOT disable the UI object, 
        // or the Start() coroutine will die and never retry.
        if (dome == null)
        {
            return;
        }

        casterAbilityHolder = dome.GetComponentInChildren<DomeAbilityHolder>();
        DomeAI domeAI = dome.GetComponentInChildren<DomeAI>();

        if (casterAbilityHolder == null || domeAI == null)
        {
            Debug.LogError("WagonHotbarManager: Dome found, but missing AbilityHolder or DomeAI.", dome.gameObject);
            return;
        }

        // Filter for abilities specifically marked as WagonCallable
        var wagonAbilities = domeAI.defaultAbilities
            .Where(a => a.usageType == AIUsageType.WagonCallable)
            .ToList();

        // --- DEBUG LOG: This will tell you if the connection works but list is empty ---
        Debug.Log($"WagonHotbarManager: Initialized. Found {wagonAbilities.Count} abilities with 'WagonCallable' usage type.");

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

    public void TriggerAbility(Ability ability)
    {
        if (casterAbilityHolder == null || !casterAbilityHolder.CanUseAbility(ability, casterAbilityHolder.gameObject)) return;

        switch (ability.abilityType)
        {
            case AbilityType.GroundAOE:
            case AbilityType.GroundPlacement:
                if (TargetingController.instance != null)
                {
                    TargetingController.instance.StartTargeting(ability, casterAbilityHolder);
                }
                else
                {
                    Debug.LogWarning("WagonHotbarManager: Missing TargetingController instance for AOE ability.");
                }
                break;

            case AbilityType.Self:
            default:
                casterAbilityHolder.UseAbility(ability, casterAbilityHolder.gameObject);
                break;
        }
    }
}