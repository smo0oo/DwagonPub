using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCPatrol : MonoBehaviour
{
    [Header("Patrol Settings")]
    public PatrolPath[] patrolPaths;

    private NavMeshAgent navMeshAgent;
    private PatrolPoint[] collectedPatrolPoints;
    private int currentPatrolIndex = 0;
    private bool isWaitingAtPatrolPoint = false;
    private float waitTimer = 0f;
    private Animator animator;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        // Settings for smoother NPC movement
        navMeshAgent.angularSpeed = 120f;
        navMeshAgent.autoBraking = true;
    }

    void Start()
    {
        CollectPatrolPoints();

        // Start moving immediately if points exist
        if (collectedPatrolPoints != null && collectedPatrolPoints.Length > 0)
        {
            navMeshAgent.SetDestination(collectedPatrolPoints[0].transform.position);
        }
    }

    void Update()
    {
        // Optional: Pause patrol if talking (requires Dialogue System integration)
        // if (PixelCrushers.DialogueSystem.DialogueManager.isConversationActive) {
        //     if (!navMeshAgent.isStopped) navMeshAgent.isStopped = true;
        //     UpdateAnimation(); 
        //     return;
        // }
        // if (navMeshAgent.isStopped) navMeshAgent.isStopped = false;

        PatrolLogic();
        UpdateAnimation();
    }

    private void PatrolLogic()
    {
        if (collectedPatrolPoints == null || collectedPatrolPoints.Length == 0) return;

        if (isWaitingAtPatrolPoint)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                isWaitingAtPatrolPoint = false;
                AdvancePatrolIndex();
                MoveToCurrentPoint();
            }
        }
        else
        {
            // Check if we reached the destination
            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                ArriveAtPoint();
            }
        }
    }

    private void ArriveAtPoint()
    {
        PatrolPoint currentPoint = collectedPatrolPoints[currentPatrolIndex];

        // 1. Play Animation
        if (!string.IsNullOrEmpty(currentPoint.animationTriggerName) && animator != null)
        {
            animator.SetTrigger(currentPoint.animationTriggerName);
        }

        // 2. Fire Unity Events (Doors, Sounds, etc.)
        if (currentPoint.onArrive != null)
        {
            currentPoint.onArrive.Invoke();
        }

        // 3. Send Message to Self (e.g. 'HealSelf')
        if (!string.IsNullOrEmpty(currentPoint.sendMessageToNPC))
        {
            SendMessage(currentPoint.sendMessageToNPC, SendMessageOptions.DontRequireReceiver);
        }

        // 4. Handle Waiting
        float waitTime = UnityEngine.Random.Range(currentPoint.minWaitTime, currentPoint.maxWaitTime);
        if (waitTime > 0)
        {
            isWaitingAtPatrolPoint = true;
            waitTimer = waitTime;
        }
        else
        {
            // Move instantly if no wait time
            AdvancePatrolIndex();
            MoveToCurrentPoint();
        }
    }

    private void MoveToCurrentPoint()
    {
        if (collectedPatrolPoints.Length == 0) return;
        navMeshAgent.SetDestination(collectedPatrolPoints[currentPatrolIndex].transform.position);
    }

    private void AdvancePatrolIndex()
    {
        PatrolPoint lastPoint = collectedPatrolPoints[currentPatrolIndex];

        if (lastPoint.nextPointOverride != null)
        {
            int nextIndex = Array.IndexOf(collectedPatrolPoints, lastPoint.nextPointOverride);
            currentPatrolIndex = (nextIndex >= 0) ? nextIndex : 0;
        }
        else if (lastPoint.jumpToRandomPoint)
        {
            currentPatrolIndex = UnityEngine.Random.Range(0, collectedPatrolPoints.Length);
        }
        else
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % collectedPatrolPoints.Length;
        }
    }

    private void CollectPatrolPoints()
    {
        if (patrolPaths == null || patrolPaths.Length == 0) return;

        List<PatrolPoint> points = new List<PatrolPoint>();

        foreach (PatrolPath path in patrolPaths)
        {
            if (path == null) continue;
            Collider pathCollider = path.GetComponent<Collider>();
            if (pathCollider == null) continue;

            Collider[] collidersInVolume = Physics.OverlapBox(
                path.transform.position,
                pathCollider.bounds.extents,
                path.transform.rotation
            );

            foreach (Collider col in collidersInVolume)
            {
                if (col.TryGetComponent<PatrolPoint>(out PatrolPoint point))
                {
                    if (!points.Contains(point)) points.Add(point);
                }
            }
        }
        // Order strictly by name to ensure consistent pathing (Point_01, Point_02...)
        collectedPatrolPoints = points.OrderBy(p => p.gameObject.name).ToArray();
    }

    // FIX: Updated to use VelocityX and VelocityZ
    private void UpdateAnimation()
    {
        if (animator != null && navMeshAgent != null)
        {
            // Convert global velocity (world space) to local space relative to the NPC's rotation
            Vector3 localVelocity = transform.InverseTransformDirection(navMeshAgent.velocity);

            // Pass the local X (sideways) and Z (forward) to the animator
            animator.SetFloat("VelocityZ", localVelocity.z);
            animator.SetFloat("VelocityX", localVelocity.x);
        }
    }
}