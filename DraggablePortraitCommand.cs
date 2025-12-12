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
        // We still try to get the instances here for efficiency.
        partyAIManager = PartyAIManager.instance;
        dragDropController = FindAnyObjectByType<UIDragDropController>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (representedPlayer == InventoryUIController.instance.ActivePlayer) return;

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

    // --- MODIFIED METHOD ---
    public void OnEndDrag(PointerEventData eventData)
    {
        if (representedPlayer == InventoryUIController.instance.ActivePlayer) return;

        // --- ADDED SAFETY CHECK ---
        // If the partyAIManager is null (due to script execution order), try to get it again.
        if (partyAIManager == null)
        {
            partyAIManager = PartyAIManager.instance;
        }
        // If it's still null, we cannot proceed. Log an error and exit.
        if (partyAIManager == null)
        {
            Debug.LogError("DraggablePortraitCommand: Could not find PartyAIManager instance!", this);
            if (dragDropController != null) dragDropController.OnEndDrag();
            StartCoroutine(UnlockInputAfterFrame());
            return;
        }
        // --- END SAFETY CHECK ---

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
                // This line will now be safe to call.
                partyAIManager.IssueCommandMoveToSingle(representedPlayer, hit.point);
            }
        }

        if (dragDropController != null)
        {
            dragDropController.OnEndDrag();
        }

        StartCoroutine(UnlockInputAfterFrame());
    }

    private IEnumerator UnlockInputAfterFrame()
    {
        yield return null;
        UIInteractionState.IsUIBlockingInput = false;
    }
}