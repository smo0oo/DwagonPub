using UnityEngine;
using UnityEngine.Events; // Required for UnityEvent

[RequireComponent(typeof(SphereCollider))]
public class PatrolPoint : MonoBehaviour
{
    [Header("Patrol Behavior")]
    [Tooltip("Minimum time in seconds the AI should wait at this point.")]
    public float minWaitTime = 0f;

    [Tooltip("Maximum time in seconds the AI should wait at this point.")]
    public float maxWaitTime = 0f;

    [Header("Pathing Override")]
    public PatrolPoint nextPointOverride;
    public bool jumpToRandomPoint = false;

    [Header("Events")]
    [Tooltip("The name of an animation trigger to play (e.g. 'Wave', 'Sit').")]
    public string animationTriggerName;

    [Tooltip("Calls a method with this name on the NPC script itself (e.g. 'HealSelf').")]
    public string sendMessageToNPC;

    [Tooltip("Generic events to fire when any NPC arrives here (e.g. Open Door, Play Sound).")]
    public UnityEvent onArrive; // The "Event" list in Inspector

private void Awake()
    {
        // This will find the SphereCollider and ensure it's a trigger.
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }
}