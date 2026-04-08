using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-100)] // Ensures this runs before Cinemachine takes control
public class SurfaceSnapper : MonoBehaviour
{
    public enum SnappingMethod
    {
        PhysicsRaycast,
        NavMeshSample
    }

    [Header("Core Settings")]
    [Tooltip("Raycast is best for visual terrain matching. NavMesh is best if the visual terrain lacks colliders.")]
    public SnappingMethod snappingMethod = SnappingMethod.PhysicsRaycast;

    [Tooltip("Offsets the pivot vertically. Leave at 0 if the wagon's pivot is exactly at the bottom of the wheels.")]
    public float verticalOffset = 0f;

    [Header("Raycast Settings")]
    [Tooltip("The layer of your terrain or ground geometry.")]
    public LayerMask groundLayer = 1 << 0; // Default layer

    [Tooltip("How high above the wagon to start looking for ground. Prevents the wagon from falling through if the spline dips underground.")]
    public float raycastStartHeight = 20f;

    [Tooltip("How far down to cast the ray.")]
    public float raycastDistance = 40f;

    [Header("Surface Alignment (AAA)")]
    [Tooltip("If true, the wagon will pitch and roll to match the slope of the hill.")]
    public bool alignToSurfaceNormal = true;

    [Tooltip("How fast the wagon tilts to match the new slope. Higher values snap instantly, lower values smooth out bumpy terrain.")]
    public float rotationLerpSpeed = 15f;

    [Header("NavMesh Settings")]
    [Tooltip("How far out to search for the nearest NavMesh surface.")]
    public float navMeshSearchRadius = 5f;

    void LateUpdate()
    {
        ExecuteSnapping();
    }

    private void ExecuteSnapping()
    {
        Vector3 currentPos = transform.position;

        if (snappingMethod == SnappingMethod.PhysicsRaycast)
        {
            // Start the ray high above the wagon's current X/Z position
            Vector3 rayOrigin = new Vector3(currentPos.x, currentPos.y + raycastStartHeight, currentPos.z);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
            {
                // Apply the new Y position while retaining the spline's X and Z
                transform.position = new Vector3(currentPos.x, hit.point.y + verticalOffset, currentPos.z);

                if (alignToSurfaceNormal)
                {
                    AlignToNormal(hit.normal);
                }
            }
        }
        else if (snappingMethod == SnappingMethod.NavMeshSample)
        {
            if (NavMesh.SamplePosition(currentPos, out NavMeshHit navHit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                transform.position = new Vector3(currentPos.x, navHit.position.y + verticalOffset, currentPos.z);

                // Note: NavMesh doesn't natively expose surface normals easily. 
                // If using NavMesh, the wagon will stay completely horizontal.
            }
        }
    }

    private void AlignToNormal(Vector3 surfaceNormal)
    {
        // Project the wagon's current forward direction flat onto the angle of the hill
        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;

        if (projectedForward != Vector3.zero)
        {
            // Create a target rotation that looks in the spline's direction, but tilts to the hill's normal
            Quaternion targetRotation = Quaternion.LookRotation(projectedForward, surfaceNormal);

            // Smoothly blend to prevent jitter on highly jagged terrain geometry
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
        }
    }
}