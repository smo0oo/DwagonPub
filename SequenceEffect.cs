using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class SequenceEffect : IAbilityEffect
{
    [Header("Activation")]
    [Range(0f, 100f)] public float chance = 100f;

    [SerializeReference]
    public List<SequenceAction> actions = new List<SequenceAction>();

    public void Apply(GameObject caster, GameObject target)
    {
        // 1. Chance Check
        if (chance < 100f && Random.Range(0f, 100f) > chance)
        {
            return;
        }

        // Find the correct MonoBehaviour to start the coroutine on.
        var playerHolder = caster.GetComponentInChildren<PlayerAbilityHolder>();
        if (playerHolder != null)
        {
            playerHolder.StartCoroutine(ExecuteSequence(playerHolder, caster, target));
            return;
        }

        var enemyHolder = caster.GetComponentInChildren<EnemyAbilityHolder>();
        if (enemyHolder != null)
        {
            enemyHolder.StartCoroutine(ExecuteSequence(enemyHolder, caster, target));
            return;
        }

        var domeHolder = caster.GetComponentInChildren<DomeAbilityHolder>();
        if (domeHolder != null)
        {
            domeHolder.StartCoroutine(ExecuteSequence(domeHolder, caster, target));
            return;
        }
    }

    private IEnumerator ExecuteSequence(MonoBehaviour owner, GameObject caster, GameObject target)
    {
        // Safety check if caster was destroyed during the delay
        if (caster == null) yield break;

        var enemyAI = caster.GetComponent<EnemyAI>();

        try
        {
            // Tell the EnemyAI to pause its brain.
            if (enemyAI != null)
            {
                enemyAI.IsInActionSequence = true;
            }

            // Execute all actions in the sequence.
            foreach (var action in actions)
            {
                if (action != null)
                {
                    yield return owner.StartCoroutine(action.Execute(owner, caster, target));
                }
            }
        }
        finally
        {
            // This 'finally' block GUARANTEES the AI will be un-paused, even if an error occurs.
            if (enemyAI != null)
            {
                enemyAI.IsInActionSequence = false;
            }
        }
    }

    public string GetEffectDescription()
    {
        string prefix = "";
        if (chance < 100f)
        {
            prefix = $"{chance}% Chance to ";
        }
        return $"{prefix}trigger a sequence of actions.";
    }
}