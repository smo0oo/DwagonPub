using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class CharacterMovementHandler : MonoBehaviour, IMovementHandler
{
    [Header("Movement Settings")]
    public float rotationSpeed = 10f;

    // Default to true. Enemies use this. Players will auto-disable it.
    public bool handleStandardMovement = true;

    public bool IsSpecialMovementActive { get; private set; } = false;

    private NavMeshAgent navAgent;
    private Animator animator;
    private int speedHash;
    private CharacterRoot myRoot;

    void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        myRoot = GetComponent<CharacterRoot>() ?? GetComponentInParent<CharacterRoot>();
        speedHash = Animator.StringToHash("Speed");

        // --- FIX: Don't overwrite speed. Use Inspector value. ---
        if (navAgent != null)
        {
            navAgent.updateRotation = false;
        }

        // --- FIX: Auto-detect Player to stop conflict ---
        if (GetComponent<PlayerMovement>() != null)
        {
            handleStandardMovement = false;
        }
    }

    void Update()
    {
        // If we are doing a special move (Leap/Charge), we control the character.
        // If not, and handleStandardMovement is FALSE (Player), we do nothing and let PlayerMovement take over.
        if (IsSpecialMovementActive) return;
        if (!handleStandardMovement) return;

        HandleStandardMovement();
    }

    private void HandleStandardMovement()
    {
        if (navAgent == null) return;

        // 1. Rotation
        if (navAgent.hasPath && navAgent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 direction = navAgent.velocity.normalized;
            direction.y = 0;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }

        // 2. Animation (Only for generic mobs using "Speed")
        if (animator != null)
        {
            float speedPercent = navAgent.velocity.magnitude / navAgent.speed;
            animator.SetFloat(speedHash, speedPercent, 0.1f, Time.deltaTime);
        }
    }

    // --- Interface Implementation: Teleport ---
    public void ExecuteTeleport(Vector3 destination)
    {
        if (IsSpecialMovementActive)
        {
            StopAllCoroutines();
            IsSpecialMovementActive = false;
            if (navAgent != null) navAgent.enabled = true;
        }

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(destination);
        }
        else
        {
            transform.position = destination;
        }
    }

    public void ExecuteLeap(Vector3 destination, System.Action onLandAction)
    {
        if (IsSpecialMovementActive) return;
        StartCoroutine(LeapRoutine(destination, onLandAction));
    }

    private IEnumerator LeapRoutine(Vector3 destination, System.Action onLandAction)
    {
        IsSpecialMovementActive = true;
        if (navAgent != null) navAgent.enabled = false;

        float flightDuration = 0.8f;
        Vector3 startPos = transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < flightDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / flightDuration;

            Vector3 currentPos = Vector3.Lerp(startPos, destination, t);
            float height = Mathf.Sin(t * Mathf.PI) * 2.5f;
            currentPos.y += height;

            transform.position = currentPos;

            Vector3 dir = (destination - startPos).normalized;
            if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

            yield return null;
        }

        transform.position = destination;
        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
        }

        IsSpecialMovementActive = false;
        onLandAction?.Invoke();
    }

    public void ExecuteCharge(GameObject target, Ability chargeAbility)
    {
        if (IsSpecialMovementActive || target == null) return;
        StartCoroutine(ChargeRoutine(target, chargeAbility));
    }

    private IEnumerator ChargeRoutine(GameObject target, Ability ability)
    {
        IsSpecialMovementActive = true;
        if (navAgent != null) navAgent.enabled = false;

        float chargeSpeed = 20f;
        float stopDistance = 1.5f;
        float maxTime = 2.0f;
        float timer = 0f;

        while (target != null && timer < maxTime)
        {
            timer += Time.deltaTime;
            float dist = Vector3.Distance(transform.position, target.transform.position);

            if (dist <= stopDistance) break;

            Vector3 dir = (target.transform.position - transform.position).normalized;
            transform.position += dir * chargeSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir);

            yield return null;
        }

        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
        }

        IsSpecialMovementActive = false;

        if (ability != null && target != null)
        {
            GameObject caster = myRoot != null ? myRoot.gameObject : gameObject;
            foreach (var effect in ability.hostileEffects)
            {
                effect.Apply(caster, target);
            }
        }
    }
}