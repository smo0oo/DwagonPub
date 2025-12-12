using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using DG.Tweening;

[RequireComponent(typeof(NavMeshAgent))]
public class CharacterMovementHandler : MonoBehaviour
{
    [Header("Leap Settings")]
    public float leapPower = 5f;
    public float leapDuration = 0.5f;

    [Header("Charge Settings")]
    public float chargeSpeed = 20f;
    public float chargeAcceleration = 50f;

    public bool IsSpecialMovementActive { get; private set; } = false;

    private NavMeshAgent navMeshAgent;
    private Collider mainCollider;
    private CharacterRoot characterRoot;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        mainCollider = GetComponent<Collider>();
        characterRoot = GetComponent<CharacterRoot>();
    }

    public Coroutine ExecuteRelativeMove(Vector3 offset, float duration)
    {
        if (IsSpecialMovementActive) return null;
        return StartCoroutine(RelativeMoveCoroutine(offset, duration));
    }

    private IEnumerator RelativeMoveCoroutine(Vector3 offset, float duration)
    {
        IsSpecialMovementActive = true;

        try
        {
            if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath();

            Vector3 worldOffset = transform.TransformDirection(offset);
            Vector3 destination = transform.position + worldOffset;

            if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
            {
                destination = hit.position;
            }
            else
            {
                destination = transform.position; // Stay in place if destination is invalid
            }

            if (navMeshAgent.isOnNavMesh) navMeshAgent.enabled = false;

            float elapsedTime = 0f;
            Vector3 startPosition = transform.position;
            while (elapsedTime < duration)
            {
                transform.position = Vector3.Lerp(startPosition, destination, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            transform.position = destination;
        }
        finally
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = true;
                if (navMeshAgent.isOnNavMesh) navMeshAgent.Warp(transform.position);
            }
            IsSpecialMovementActive = false;
        }
    }

    public void ExecuteLeap(Vector3 destination, Action onLandAction)
    {
        if (IsSpecialMovementActive) return;
        StartCoroutine(LeapCoroutine(destination, onLandAction));
    }

    private IEnumerator LeapCoroutine(Vector3 destination, Action onLandAction)
    {
        IsSpecialMovementActive = true;

        // Disabling the agent is correct for a jump/leap
        if (navMeshAgent.isOnNavMesh) navMeshAgent.enabled = false;

        transform.DOJump(destination, leapPower, 1, leapDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                onLandAction?.Invoke();
                if (navMeshAgent)
                {
                    navMeshAgent.enabled = true;
                    if (navMeshAgent.isOnNavMesh) navMeshAgent.Warp(transform.position);
                }

                IsSpecialMovementActive = false;
            });

        yield return new WaitForSeconds(leapDuration);
    }

    public void ExecuteCharge(GameObject target, Ability chargeAbility)
    {
        if (IsSpecialMovementActive) return;
        StartCoroutine(ChargeCoroutine(target, chargeAbility));
    }

    // --- UPDATED METHOD ---
    private IEnumerator ChargeCoroutine(GameObject target, Ability chargeAbility)
    {
        IsSpecialMovementActive = true;

        // 1. Capture current state (Was it false because of WASD mode?)
        bool wasUpdatePosition = navMeshAgent.updatePosition;
        float originalSpeed = navMeshAgent.speed;
        float originalAccel = navMeshAgent.acceleration;

        // 2. Force settings for the charge
        navMeshAgent.speed = chargeSpeed;
        navMeshAgent.acceleration = chargeAcceleration;
        navMeshAgent.stoppingDistance = 2f;

        // This is the FIX: Allow the agent to move the transform during the charge
        navMeshAgent.updatePosition = true;

        while (target != null && Vector3.Distance(transform.position, target.transform.position) > navMeshAgent.stoppingDistance)
        {
            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.SetDestination(target.transform.position);
            }
            yield return null;
        }

        if (target != null && characterRoot != null)
        {
            foreach (var effect in chargeAbility.hostileEffects)
            {
                effect.Apply(characterRoot.gameObject, target);
            }
        }

        // 3. Restore original settings
        navMeshAgent.speed = originalSpeed;
        navMeshAgent.acceleration = originalAccel;

        // Restore the mode (if it was WASD, this sets it back to false)
        navMeshAgent.updatePosition = wasUpdatePosition;

        if (navMeshAgent.isOnNavMesh) navMeshAgent.ResetPath();

        IsSpecialMovementActive = false;
    }
}