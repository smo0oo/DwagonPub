using UnityEngine;

/// <summary>
/// A marker component for a GameObject that represents a patrol area.
/// This GameObject should have a trigger collider (e.g., a BoxCollider).
/// The EnemyAI will find all PatrolPoint components within this trigger volume.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PatrolPath : MonoBehaviour
{
    private void Awake()
    {
        // Ensure the collider is set to be a trigger so it doesn't block anything.
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"The collider on PatrolPath '{gameObject.name}' is not set to 'Is Trigger'. It has been set automatically.", this);
            col.isTrigger = true;
        }
    }
}