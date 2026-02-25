using UnityEngine;
using PixelCrushers.DialogueSystem;

[RequireComponent(typeof(BoxCollider))]
public class DestroyOnTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("Select the Layer(s) that will trigger this. e.g., 'Player'")]
    [SerializeField] private LayerMask targetLayer;

    [Header("Quest Updates")]
    [Tooltip("Exact name of the quest as it appears in your Dialogue Database")]
    public string questName;
    [Tooltip("What state should the quest switch to?")]
    public QuestState newQuestState = QuestState.Success;

    [Header("Editor Visuals")]
    public Color gizmoColor = new Color(1f, 0.6f, 0.8f, 0.4f); // Soft Pink with 40% opacity

    private void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // AAA FIX: Explicitly ignore ClothPhysics so the trigger doesn't double-fire if the mask ever overlaps
        if (other.gameObject.layer == LayerMask.NameToLayer("ClothPhysics")) return;

        if (IsInLayerMask(other.gameObject.layer, targetLayer))
        {
            if (!string.IsNullOrEmpty(questName))
            {
                QuestLog.SetQuestState(questName, newQuestState);
                DialogueManager.ShowAlert($"Quest Update: {questName}");
            }

            Destroy(gameObject);
        }
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    // --- VISUALIZATION LOGIC ---
    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            // Set the matrix so the gizmo rotates/scales with the object
            Gizmos.matrix = transform.localToWorldMatrix;

            // Draw the filled box (Transparent)
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(box.center, box.size);

            // Draw the outline (Solid)
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}