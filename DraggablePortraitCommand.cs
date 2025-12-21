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
        // 1. Scene Rule Check: If switching is disabled (Town/WorldMap), block all dragging.
        if (PartyManager.instance != null && !PartyManager.instance.playerSwitchingEnabled)
        {
            return;
        }

        // 2. Active Player Check: Do not drag the portrait of the currently controlled player.
        if (representedPlayer == InventoryUIController.instance.ActivePlayer) return;

        // --- NEW: Inactive Member Check ---
        // 3. Activity Check: If the character is disabled (e.g., Player 0 in a Dungeon), block dragging.
        if (representedPlayer != null && !representedPlayer.activeInHierarchy)
        {
            return;
        }

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

        if (representedPlayer == InventoryUIController.instance.ActivePlayer ||
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

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.layer == LayerMask.NameToLayer("Enemy"))
            {
                partyAIManager.IssueCommandAttackSingle(representedPlayer, hitObject);
            }
            else if (hitObject.layer == LayerMask.NameToLayer("Terrain"))
            {
                partyAIManager.IssueCommandMoveToSingle(representedPlayer, hit.point);
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