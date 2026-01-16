using UnityEngine;
using UnityEngine.AI;
using System.Linq;

// --- IDLE STATE (Patrolling & Scanning) ---
public class IdleState : IEnemyState
{
    private float waitTimer = 0f;

    public void Enter(EnemyAI enemy)
    {
        enemy.SetAIStatus("Idle", "Scanning");
        // Ensure we don't carry over momentum or old paths
        if (enemy.NavAgent != null && enemy.NavAgent.isActiveAndEnabled && enemy.NavAgent.isOnNavMesh)
        {
            enemy.NavAgent.ResetPath();
        }
    }

    public void Execute(EnemyAI enemy)
    {
        // 1. Look for Target
        enemy.currentTarget = enemy.Targeting.FindBestTarget(null);

        if (enemy.currentTarget != null)
        {
            enemy.SwitchState(new CombatState());
            return;
        }

        // 2. Patrol Logic
        if (enemy.CollectedPatrolPoints == null || enemy.CollectedPatrolPoints.Length == 0)
        {
            enemy.SetAIStatus("Idle", "Searching...");
            return;
        }

        // Check if we are physically waiting at a point
        if (enemy.IsWaitingAtPatrolPoint)
        {
            enemy.SetAIStatus("Idle", "Waiting");
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                enemy.IsWaitingAtPatrolPoint = false;
                AdvancePatrolIndex(enemy);
                MoveToCurrentPatrolPoint(enemy);
            }
        }
        // Check if we have arrived (Remaining Distance < Threshold)
        else if (enemy.NavAgent.isOnNavMesh && !enemy.NavAgent.pathPending && enemy.NavAgent.remainingDistance < 0.5f)
        {
            // If we have no path but aren't waiting, we might have just started or finished
            if (enemy.NavAgent.hasPath || enemy.NavAgent.velocity.sqrMagnitude < 0.1f)
            {
                ArriveAtPatrolPoint(enemy);
            }
        }
        else
        {
            enemy.SetAIStatus("Idle", "Patrolling");
        }
    }

    public void Exit(EnemyAI enemy) { }

    private void ArriveAtPatrolPoint(EnemyAI enemy)
    {
        PatrolPoint currentPoint = enemy.CollectedPatrolPoints[enemy.CurrentPatrolIndex];

        if (!string.IsNullOrEmpty(currentPoint.animationTriggerName) && enemy.Animator != null)
        {
            enemy.Animator.SetTrigger(currentPoint.animationTriggerName);
        }

        currentPoint.onArrive?.Invoke();

        if (!string.IsNullOrEmpty(currentPoint.sendMessageToNPC))
        {
            enemy.SendMessage(currentPoint.sendMessageToNPC, SendMessageOptions.DontRequireReceiver);
        }

        float waitTime = Random.Range(currentPoint.minWaitTime, currentPoint.maxWaitTime);
        if (waitTime > 0)
        {
            enemy.IsWaitingAtPatrolPoint = true;
            waitTimer = waitTime;
        }
        else
        {
            AdvancePatrolIndex(enemy);
            MoveToCurrentPatrolPoint(enemy);
        }
    }

    private void AdvancePatrolIndex(EnemyAI enemy)
    {
        PatrolPoint lastPoint = enemy.CollectedPatrolPoints[enemy.CurrentPatrolIndex];
        if (lastPoint.nextPointOverride != null)
        {
            int nextIndex = System.Array.IndexOf(enemy.CollectedPatrolPoints, lastPoint.nextPointOverride);
            enemy.CurrentPatrolIndex = (nextIndex >= 0) ? nextIndex : 0;
        }
        else if (lastPoint.jumpToRandomPoint)
        {
            enemy.CurrentPatrolIndex = Random.Range(0, enemy.CollectedPatrolPoints.Length);
        }
        else
        {
            enemy.CurrentPatrolIndex = (enemy.CurrentPatrolIndex + 1) % enemy.CollectedPatrolPoints.Length;
        }
    }

    private void MoveToCurrentPatrolPoint(EnemyAI enemy)
    {
        if (enemy.CollectedPatrolPoints.Length > 0 && enemy.NavAgent.isOnNavMesh)
        {
            enemy.NavAgent.SetDestination(enemy.CollectedPatrolPoints[enemy.CurrentPatrolIndex].transform.position);
        }
    }
}

// --- COMBAT STATE (Chasing & Attacking) ---
public class CombatState : IEnemyState
{
    private float lastLostSightCheckTime;
    private bool cachedHasLOS;
    private float timeSinceLostSight;
    private Vector3 lastKnownPosition;
    private const float LOS_CHECK_INTERVAL = 0.2f;

    public void Enter(EnemyAI enemy)
    {
        enemy.SetAIStatus("Combat", "Engaged");
        enemy.CombatStartPosition = enemy.transform.position;
        enemy.HasUsedInitialAbilities = false;

        if (enemy.currentTarget != null)
            lastKnownPosition = enemy.currentTarget.position;
    }

    public void Execute(EnemyAI enemy)
    {
        // 1. Check Leash
        if (enemy.currentTarget == null || Vector3.Distance(enemy.transform.position, enemy.CombatStartPosition) > enemy.chaseLeashRadius)
        {
            enemy.SwitchState(new ReturnState());
            return;
        }

        // 2. Check Invalid Target
        if (enemy.IsTargetInvalid(enemy.currentTarget))
        {
            enemy.currentTarget = null;
            enemy.SwitchState(new IdleState());
            return;
        }

        // 3. Check Retreat Condition
        if (enemy.Health.maxHealth > 0)
        {
            float hpPercent = (float)enemy.Health.currentHealth / enemy.Health.maxHealth;
            if (hpPercent < enemy.retreatHealthThreshold)
            {
                if (Random.value < enemy.retreatChance)
                {
                    enemy.SwitchState(new RetreatState());
                    return;
                }
            }
        }

        // 4. LOS Logic
        float distToTarget = Vector3.Distance(enemy.transform.position, enemy.currentTarget.position);
        if (Time.time - lastLostSightCheckTime > LOS_CHECK_INTERVAL || distToTarget < enemy.meleeAttackRange)
        {
            cachedHasLOS = enemy.HasLineOfSight(enemy.currentTarget);
            lastLostSightCheckTime = Time.time;
        }

        if (cachedHasLOS)
        {
            lastKnownPosition = enemy.currentTarget.position;
            timeSinceLostSight = 0f;

            // 5. Select & Use Ability
            Ability abilityToUse = enemy.AbilitySelector.SelectBestAbility(enemy.currentTarget, enemy.HasUsedInitialAbilities);
            bool isReady = abilityToUse != null && enemy.AbilityHolder.CanUseAbility(abilityToUse, enemy.currentTarget.gameObject);
            float effectiveRange = (abilityToUse != null) ? abilityToUse.range : enemy.meleeAttackRange;

            // Attack Logic
            if (isReady && distToTarget <= effectiveRange)
            {
                enemy.StopMovement(); // Uses ResetPath now
                enemy.SetAIStatus("Combat", $"Attacking: {abilityToUse.displayName}");
                enemy.PerformAttack(abilityToUse);

                if (enemy.AbilitySelector.initialAbilities.Contains(abilityToUse))
                    enemy.HasUsedInitialAbilities = true;
            }
            else
            {
                // Movement Logic
                HandleCombatMovement(enemy, isReady, abilityToUse);
            }
        }
        else
        {
            // Lost Sight
            if (enemy.isTimid)
            {
                enemy.currentTarget = null;
                enemy.SwitchState(new IdleState());
                return;
            }

            timeSinceLostSight += Time.deltaTime;
            enemy.SetAIStatus("Combat", "Searching...");

            if (timeSinceLostSight > enemy.lostSightSearchDuration)
            {
                enemy.currentTarget = null; // Give up
            }
            else
            {
                enemy.NavAgent.SetDestination(lastKnownPosition);
            }
        }
    }

    public void Exit(EnemyAI enemy)
    {
        enemy.ResetCombatState();
    }

    private void HandleCombatMovement(EnemyAI enemy, bool isReady, Ability abilityToUse)
    {
        bool isTargetDomeMarker = enemy.currentTarget.CompareTag("DomeMarker");

        if (isTargetDomeMarker)
        {
            ExecuteDirectMovement(enemy);
        }
        else if (isReady && enemy.archetype == AIArchetype.Melee)
        {
            ExecuteClosingMovement(enemy, abilityToUse);
        }
        else
        {
            switch (enemy.archetype)
            {
                case AIArchetype.Melee: ExecuteMeleeMovement(enemy); break;
                case AIArchetype.Ranged: ExecuteRangedMovement(enemy); break;
                case AIArchetype.Hybrid:
                    if (isReady && abilityToUse.range <= enemy.meleeAttackRange)
                        ExecuteClosingMovement(enemy, abilityToUse);
                    else
                        ExecuteRangedMovement(enemy);
                    break;
            }
        }
    }

    private void ExecuteClosingMovement(EnemyAI enemy, Ability ability)
    {
        enemy.NavAgent.speed = enemy.OriginalSpeed;
        if (enemy.AssignedSurroundPoint != null)
        {
            SurroundPointManager.instance.ReleasePoint(enemy);
            enemy.AssignedSurroundPoint = null;
        }

        float range = (ability != null) ? ability.range : enemy.meleeAttackRange;
        enemy.NavAgent.stoppingDistance = range * 0.8f;
        enemy.NavAgent.SetDestination(enemy.currentTarget.position);
        enemy.SetAIStatus("Combat", "Closing In");
    }

    private void ExecuteDirectMovement(EnemyAI enemy)
    {
        enemy.NavAgent.speed = enemy.OriginalSpeed;
        enemy.NavAgent.stoppingDistance = enemy.meleeAttackRange * 0.8f;
        enemy.NavAgent.SetDestination(enemy.currentTarget.position);
        enemy.SetAIStatus("Combat", "Advancing on Dome");
    }

    private void ExecuteMeleeMovement(EnemyAI enemy)
    {
        enemy.NavAgent.speed = enemy.OriginalSpeed;
        if (enemy.AssignedSurroundPoint == null)
            enemy.AssignedSurroundPoint = SurroundPointManager.instance.RequestPoint(enemy, enemy.currentTarget);

        Vector3 destination = enemy.AssignedSurroundPoint != null ? enemy.AssignedSurroundPoint.position : enemy.currentTarget.position;
        enemy.NavAgent.stoppingDistance = 0.5f;
        enemy.NavAgent.SetDestination(destination);
        enemy.SetAIStatus("Combat", enemy.AssignedSurroundPoint != null ? "Circling" : "Waiting");
    }

    private void ExecuteRangedMovement(EnemyAI enemy)
    {
        float distanceToTarget = Vector3.Distance(enemy.transform.position, enemy.currentTarget.position);
        if (distanceToTarget < enemy.minimumRangedAttackRange)
        {
            enemy.NavAgent.speed = enemy.retreatAndKiteSpeed;
            enemy.RetreatFromTarget();
        }
        else
        {
            enemy.NavAgent.speed = enemy.OriginalSpeed;
            if (distanceToTarget > enemy.preferredCombatRange)
            {
                Vector3 dest = enemy.currentTarget.position - (enemy.currentTarget.position - enemy.transform.position).normalized * enemy.preferredCombatRange;
                enemy.NavAgent.SetDestination(dest);
                enemy.SetAIStatus("Combat", "Advancing");
            }
            else
            {
                // In Range
                enemy.NavAgent.ResetPath();
                enemy.SetAIStatus("Combat", "In Range");
            }
        }
    }
}

// --- RETREAT STATE (Running Away) ---
public class RetreatState : IEnemyState
{
    private float timer;

    public void Enter(EnemyAI enemy)
    {
        enemy.SetAIStatus("Retreating", "Fleeing");
        timer = enemy.retreatDuration;
        enemy.NavAgent.speed = enemy.retreatAndKiteSpeed;
        if (enemy.AssignedSurroundPoint != null)
        {
            SurroundPointManager.instance.ReleasePoint(enemy);
            enemy.AssignedSurroundPoint = null;
        }
    }

    public void Execute(EnemyAI enemy)
    {
        timer -= Time.deltaTime;
        if (timer > 0 && enemy.currentTarget != null)
        {
            enemy.RetreatFromTarget();
        }
        else
        {
            enemy.SwitchState(new CombatState());
        }
    }

    public void Exit(EnemyAI enemy)
    {
        enemy.NavAgent.speed = enemy.OriginalSpeed;
    }
}

// --- RETURN STATE (Leashing) ---
public class ReturnState : IEnemyState
{
    public void Enter(EnemyAI enemy)
    {
        enemy.SetAIStatus("Returning", "Leashing");
        enemy.ResetCombatState();
        enemy.NavAgent.speed = enemy.OriginalSpeed;
        enemy.NavAgent.stoppingDistance = 0.5f; // Ensure we go all the way back
        enemy.NavAgent.SetDestination(enemy.StartPosition);
    }

    public void Execute(EnemyAI enemy)
    {
        // Check if we found a new target while returning
        enemy.currentTarget = enemy.Targeting.FindBestTarget(null);
        if (enemy.currentTarget != null)
        {
            enemy.SwitchState(new CombatState());
            return;
        }

        // Check arrival
        if (enemy.NavAgent.isOnNavMesh && !enemy.NavAgent.pathPending && enemy.NavAgent.remainingDistance <= 0.5f)
        {
            enemy.NavAgent.ResetPath();
            enemy.SwitchState(new IdleState());
        }
    }

    public void Exit(EnemyAI enemy) { }
}