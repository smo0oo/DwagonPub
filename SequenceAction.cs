using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[System.Serializable]
public abstract class SequenceAction
{
    public abstract IEnumerator Execute(MonoBehaviour owner, GameObject caster, GameObject target);
}

[System.Serializable]
public class UseAbilityAction : SequenceAction
{
    public Ability abilityToCall;
    [Range(0f, 1f)]
    public float triggerChance = 1f;
    public bool overrideCooldown = false;

    public override IEnumerator Execute(MonoBehaviour owner, GameObject caster, GameObject target)
    {
        if (Random.value > triggerChance) yield break;

        var playerHolder = caster.GetComponentInChildren<PlayerAbilityHolder>();
        if (playerHolder != null)
        {
            playerHolder.UseAbility(abilityToCall, target, overrideCooldown);
            yield break;
        }

        var enemyHolder = caster.GetComponentInChildren<EnemyAbilityHolder>();
        if (enemyHolder != null)
        {
            enemyHolder.UseAbility(abilityToCall, target, overrideCooldown);
            yield break;
        }

        var domeHolder = caster.GetComponentInChildren<DomeAbilityHolder>();
        if (domeHolder != null)
        {
            domeHolder.UseAbility(abilityToCall, target, overrideCooldown);
            yield break;
        }
    }
}

/// <summary>
/// Forces the character to slide/dash to a location manually.
/// Good for: Dodges, Lunges, Knockbacks.
/// Bad for: Walking, Pathfinding around walls.
/// </summary>
[System.Serializable]
public class MoveRelativeAction : SequenceAction
{
    public Vector3 moveOffset;
    public float duration = 0.5f;
    public bool faceMovementDirection = true;

    public override IEnumerator Execute(MonoBehaviour owner, GameObject caster, GameObject target)
    {
        if (!caster.TryGetComponent<NavMeshAgent>(out var agent)) yield break;

        if (agent.isOnNavMesh) agent.ResetPath();

        // Calculate destination relative to current facing
        Vector3 worldOffset = caster.transform.TransformDirection(moveOffset);
        Vector3 destination = caster.transform.position + worldOffset;

        // Snap to valid NavMesh point so we don't end up inside a wall
        if (NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
        {
            destination = hit.position;
        }
        else
        {
            yield break; // Invalid location
        }

        // Disable agent to override physics
        agent.enabled = false;

        float elapsedTime = 0f;
        Vector3 startPosition = caster.transform.position;
        Quaternion startRotation = caster.transform.rotation;
        Quaternion targetRotation = startRotation;

        if (faceMovementDirection && worldOffset.sqrMagnitude > 0.01f)
        {
            targetRotation = Quaternion.LookRotation(worldOffset.normalized);
        }

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            // Apply Easing for smoother dash start/stop
            float smoothT = t * t * (3f - 2f * t);

            caster.transform.position = Vector3.Lerp(startPosition, destination, smoothT);

            if (faceMovementDirection)
            {
                caster.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT * 5f);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        caster.transform.position = destination;

        agent.enabled = true;
        if (agent.isOnNavMesh) agent.Warp(caster.transform.position);
    }
}

/// <summary>
/// Orders the character to run/walk to a location using the NavMesh.
/// Good for: Repositioning, Flanking, Natural movement.
/// </summary>
[System.Serializable]
public class NavMeshMoveAction : SequenceAction
{
    [Header("Movement Settings")]
    [Tooltip("Target location relative to the Caster's current rotation.")]
    public Vector3 moveOffset;
    [Tooltip("If > 0, overrides the agent speed. If -1, uses current speed.")]
    public float speedOverride = -1f;
    [Tooltip("Max time to wait for arrival before giving up.")]
    public float timeoutDuration = 3.0f;
    [Tooltip("If true, ignores offset and moves towards the Combat Target.")]
    public bool moveToTarget = false;
    public float stoppingDistance = 1.5f;

    public override IEnumerator Execute(MonoBehaviour owner, GameObject caster, GameObject target)
    {
        if (!caster.TryGetComponent<NavMeshAgent>(out var agent)) yield break;
        if (!agent.isOnNavMesh) yield break;

        // 1. Calculate Destination
        Vector3 targetPos;

        if (moveToTarget && target != null)
        {
            targetPos = target.transform.position;
        }
        else
        {
            targetPos = caster.transform.position + caster.transform.TransformDirection(moveOffset);
        }

        // Validate Destination on NavMesh
        if (NavMesh.SamplePosition(targetPos, out var hit, 5f, NavMesh.AllAreas))
        {
            targetPos = hit.position;
        }

        // 2. Setup Agent
        float originalSpeed = agent.speed;
        float originalStoppingDist = agent.stoppingDistance;

        if (speedOverride > 0) agent.speed = speedOverride;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;

        // 3. Issue Command
        agent.SetDestination(targetPos);

        // 4. Wait for Arrival (with Timeout)
        float timer = 0f;
        while (timer < timeoutDuration)
        {
            // Check if we reached destination
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                break; // Arrived
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 5. Cleanup
        agent.isStopped = true;
        agent.ResetPath();

        // Restore original stats
        agent.speed = originalSpeed;
        agent.stoppingDistance = originalStoppingDist;
    }
}

[System.Serializable]
public class WaitAction : SequenceAction
{
    public float duration = 1.0f;

    public override IEnumerator Execute(MonoBehaviour owner, GameObject caster, GameObject target)
    {
        if (duration > 0)
        {
            yield return new WaitForSeconds(duration);
        }
    }
}