using UnityEngine;

[RequireComponent(typeof(SphereCollider))] // This now specifies a concrete collider type.
public class PatrolPoint : MonoBehaviour
{
    [Header("Patrol Behavior")]
    [Tooltip("Minimum time in seconds the AI should wait at this point.")]
    public float minWaitTime = 0f;

    [Tooltip("Maximum time in seconds the AI should wait at this point.")]
    public float maxWaitTime = 0f;

    [Header("Pathing Override")]
    [Tooltip("OPTIONAL: Forcing the AI to go to a specific point next. Overrides both linear and random pathing.")]
    public PatrolPoint nextPointOverride;

    [Tooltip("If 'Next Point Override' is not set, checking this will make the AI move to a random point from its list instead of the next one in order.")]
    public bool jumpToRandomPoint = false;

    [Header("Animation")]
    [Tooltip("The name of an animation trigger to play when the AI arrives.")]
    public string animationTriggerName;

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