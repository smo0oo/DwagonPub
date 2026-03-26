using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class CharacterMovementHandler : MonoBehaviour, IMovementHandler
{
    [Header("Movement Settings")]
    public float rotationSpeed = 10f;

    public bool handleStandardMovement = true;

    [Header("Collision Checks")]
    [Tooltip("Layers that will block the Leap and Charge abilities (e.g. Walls, Default).")]
    public LayerMask obstacleLayers;

    public bool IsSpecialMovementActive { get; private set; } = false;

    private NavMeshAgent navAgent;
    private Animator animator;

    // Animation Hashes
    private int speedHash;
    private int velocityXHash;
    private int velocityZHash;

    // Parameter Flags
    private bool hasSpeedParam = false;
    private bool hasVelocityParams = false;

    private CharacterRoot myRoot;
    private PlayerMovement playerMovement;
    private PartyMemberAI partyAI;

    void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        myRoot = GetComponent<CharacterRoot>() ?? GetComponentInParent<CharacterRoot>();

        playerMovement = GetComponent<PlayerMovement>();
        partyAI = GetComponent<PartyMemberAI>();

        if (playerMovement != null || partyAI != null)
        {
            handleStandardMovement = false;
        }

        if (animator != null)
        {
            speedHash = Animator.StringToHash("Speed");
            velocityXHash = Animator.StringToHash("VelocityX");
            velocityZHash = Animator.StringToHash("VelocityZ");

            hasSpeedParam = HasParameter(animator, speedHash);
            hasVelocityParams = HasParameter(animator, velocityXHash) && HasParameter(animator, velocityZHash);
        }

        if (navAgent != null)
        {
            navAgent.updateRotation = false;
        }
    }

    void Update()
    {
        if (IsSpecialMovementActive) return;
        if (!handleStandardMovement) return;
        if (playerMovement != null && playerMovement.enabled) return;

        HandleStandardMovement();
    }

    private void HandleStandardMovement()
    {
        if (navAgent == null) return;

        // Rotation
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

        // Animation
        if (animator != null)
        {
            float maxSpeed = navAgent.speed > 0 ? navAgent.speed : 3.5f;
            Vector3 worldVelocity = navAgent.velocity;

            // 2D Blend Tree (Prioritize if available)
            if (hasVelocityParams)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
                float vX = localVelocity.x / maxSpeed;
                float vZ = localVelocity.z / maxSpeed;

                animator.SetFloat(velocityXHash, vX, 0.1f, Time.deltaTime);
                animator.SetFloat(velocityZHash, vZ, 0.1f, Time.deltaTime);
            }
            // 1D Blend Tree (Fallback)
            else if (hasSpeedParam)
            {
                float speedPercent = worldVelocity.magnitude / maxSpeed;
                animator.SetFloat(speedHash, speedPercent, 0.1f, Time.deltaTime);
            }
        }
    }

    private bool HasParameter(Animator anim, int paramHash)
    {
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.nameHash == paramHash) return true;
        }
        return false;
    }

    // --- Interface Implementations ---
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

        Vector3 startPos = transform.position;
        // Lift origin slightly so we don't hit the floor immediately, but keep it relevant to body height
        Vector3 rayOrigin = startPos + Vector3.up * 1.0f;
        Vector3 targetPos = destination + Vector3.up * 1.0f;

        Vector3 direction = (targetPos - rayOrigin).normalized;
        float distance = Vector3.Distance(rayOrigin, targetPos);

        // Check for walls/obstacles
        if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, distance, obstacleLayers))
        {
            Vector3 hitPoint = hit.point;

            // Back off 0.5 units from the wall to prevent clipping
            Vector3 backOff = direction * 0.5f;
            hitPoint -= backOff;
            hitPoint.y = destination.y;

            if (NavMesh.SamplePosition(hitPoint, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
            {
                destination = navHit.position;
            }
            else
            {
                destination = hitPoint;
            }
        }

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

        if (navAgent != null)
        {
            navAgent.ResetPath();
            navAgent.enabled = false;
        }

        float chargeSpeed = 35f; // Fast, aggressive dash speed
        float stopDistance = 1.5f;
        float maxTime = 2.0f;
        float timer = 0f;

        while (target != null && timer < maxTime)
        {
            timer += Time.deltaTime;

            // Keep the destination perfectly flat on the ground so the character doesn't fly up into the air
            Vector3 targetPos = target.transform.position;
            targetPos.y = transform.position.y;

            float dist = Vector3.Distance(transform.position, targetPos);

            if (dist <= stopDistance) break;

            Vector3 moveDirection = (targetPos - transform.position).normalized;

            // Safety Check: Are we about to charge through a wall?
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, moveDirection, out RaycastHit hit, 1.0f, obstacleLayers))
            {
                break; // Hit a wall, stop the charge instantly!
            }

            // MoveTowards prevents overshooting the target and completely eliminates orbiting
            transform.position = Vector3.MoveTowards(transform.position, targetPos, chargeSpeed * Time.deltaTime);

            if (moveDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }

            yield return null;
        }

        // Clean snap back onto the NavMesh
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