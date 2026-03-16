using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;

public class DraggablePortraitCommand : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject representedPlayer;
    public UIPartyPortraitsManager uiManager;

    private PartyAIManager partyAIManager;
    private UIDragDropController dragDropController;
    private Image portraitImage;

    void Awake()
    {
        partyAIManager = PartyAIManager.instance;
        dragDropController = FindAnyObjectByType<UIDragDropController>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 1. Scene Rule Check
        if (PartyManager.instance != null && !PartyManager.instance.playerSwitchingEnabled) return;

        // --- AAA FIX: Safely ask PartyManager instead of InventoryUIController ---
        if (PartyManager.instance != null && representedPlayer == PartyManager.instance.ActivePlayer) return;

        // 3. Activity Check
        if (representedPlayer != null && !representedPlayer.activeInHierarchy) return;

        if (uiManager != null && representedPlayer != null)
        {
            portraitImage = uiManager.GetPortraitUIForPlayer(representedPlayer)?.portraitImage;
        }

        UIInteractionState.IsUIBlockingInput = true;

        if (dragDropController != null && portraitImage != null)
        {
            dragDropController.OnBeginDrag(null, portraitImage.sprite);
        }
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Safety checks for scene rules and active state
        if (PartyManager.instance != null && !PartyManager.instance.playerSwitchingEnabled)
        {
            CancelDrag();
            return;
        }

        if ((PartyManager.instance != null && representedPlayer == PartyManager.instance.ActivePlayer) ||
            (representedPlayer != null && !representedPlayer.activeInHierarchy))
        {
            CancelDrag();
            return;
        }

        if (partyAIManager == null) partyAIManager = PartyAIManager.instance;

        if (partyAIManager == null)
        {
            Debug.LogError("DraggablePortraitCommand: Could not find PartyAIManager instance!", this);
            CancelDrag();
            return;
        }

        // --- AAA FIX: Use EventSystem Pointer Data (Input System Agnostic) ---
        Ray ray = Camera.main.ScreenPointToRay(eventData.position);

        // --- AAA FIX: RaycastAll pierces invisible triggers to find the actual targets ---
        RaycastHit[] hits = Physics.RaycastAll(ray, 200f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool commandIssued = false;

        // Pass 1: Prioritize Enemies (In case an enemy is standing on terrain, attack them instead of moving)
        foreach (var hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;
            if (hitObject.layer == LayerMask.NameToLayer("Enemy"))
            {
                partyAIManager.IssueCommandAttackSingle(representedPlayer, hitObject);
                commandIssued = true;
                break;
            }
        }

        // Pass 2: If we didn't hit an enemy, find the ground terrain to move to
        if (!commandIssued)
        {
            foreach (var hit in hits)
            {
                GameObject hitObject = hit.collider.gameObject;
                if (hitObject.layer == LayerMask.NameToLayer("Terrain"))
                {
                    partyAIManager.IssueCommandMoveToSingle(representedPlayer, hit.point);
                    break;
                }
            }
        }

        if (dragDropController != null) dragDropController.OnEndDrag();
        StartCoroutine(UnlockInputAfterFrame());
    }

    private void CancelDrag()
    {
        if (dragDropController != null) dragDropController.OnEndDrag();
        StartCoroutine(UnlockInputAfterFrame());
    }

    private IEnumerator UnlockInputAfterFrame()
    {
        yield return null;
        UIInteractionState.IsUIBlockingInput = false;
    }
}