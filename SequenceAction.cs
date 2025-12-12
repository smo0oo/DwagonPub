using UnityEngine;
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

        // --- CORRECTED LOGIC ---
        // This now correctly searches the hierarchy for the right ability holder.
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

[System.Serializable]
public class MoveRelativeAction : SequenceAction
{
    public Vector3 moveOffset;
    public float duration = 0.5f;

    public override IEnumerator Execute(MonoBehaviour owner, GameObject caster, GameObject target)
    {
        if (!caster.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent)) yield break;

        // --- ADDED: Clear the agent's current path to prevent conflicts ---
        if (agent.isOnNavMesh) agent.ResetPath();

        Vector3 worldOffset = caster.transform.TransformDirection(moveOffset);
        Vector3 destination = caster.transform.position + worldOffset;

        if (UnityEngine.AI.NavMesh.SamplePosition(destination, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            destination = hit.position;
        }
        else
        {
            yield break;
        }

        // The rest of the logic can now run safely because the EnemyAI is paused.
        agent.enabled = false;

        float elapsedTime = 0f;
        Vector3 startPosition = caster.transform.position;
        while (elapsedTime < duration)
        {
            caster.transform.position = Vector3.Lerp(startPosition, destination, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        caster.transform.position = destination;

        agent.enabled = true;
        if (agent.isOnNavMesh) agent.Warp(caster.transform.position);
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