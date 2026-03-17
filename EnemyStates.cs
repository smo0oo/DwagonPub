using UnityEngine;
using UnityEngine.AI;

// --- IDLE STATE (Patrolling & Scanning) ---
public class IdleState : IEnemyState
{
    private float waitTimer = 0f;

    public void Enter(EnemyAI enemy)
    {
        enemy.SetAIStatus("Idle", "Scanning");
        if (enemy.NavAgent != null && enemy.NavAgent.isActiveAndEnabled && enemy.NavAgent.isOnNavMesh)
        {
            enemy.NavAgent.ResetPath();
        }
    }

    public void Execute(EnemyAI enemy)
    {
        enemy.currentTarget = enemy.Targeting.FindBestTarget(null);

        if (enemy.currentTarget != null)
        {
            enemy.SwitchState(new CombatState());
            return;
        }

        if (enemy.CollectedPatrolPoints == null || enemy.CollectedPatrolPoints.Length == 0)
        {
            enemy.SetAIStatus("Idle", "Searching...");
            return;
        }

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
        else if (enemy.NavAgent.isOnNavMesh && !enemy.NavAgent.pathPending && enemy.NavAgent.remainingDistance < 0.5f)
        {
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
        if (enemy.currentTarget == null || Vector3.Distance(enemy.transform.position, enemy.CombatStartPosition) > enemy.chaseLeashRadius)
        {
            enemy.SwitchState(new ReturnState());
            return;
        }

        if (enemy.IsTargetInvalid(enemy.currentTarget))
        {
            enemy.currentTarget = null;
            enemy.SwitchState(new IdleState());
            return;
        }

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

            Ability abilityToUse = enemy.AbilitySelector.SelectBestAbility(enemy.currentTarget, enemy.HasUsedInitialAbilities);
            bool isReady = abilityToUse != null && enemy.AbilityHolder.CanUseAbility(abilityToUse, enemy.currentTarget.gameObject);
            float effectiveRange = (abilityToUse != null) ? abilityToUse.range : enemy.meleeAttackRange;

            if (isReady && distToTarget <= effectiveRange)
            {
                enemy.StopMovement();
                enemy.SetAIStatus("Combat", $"Attacking: {abilityToUse.displayName}");
                enemy.PerformAttack(abilityToUse);

                if (enemy.AbilitySelector.initialAbilities.Contains(abilityToUse))
                    enemy.HasUsedInitialAbilities = true;
            }
            else
            {
                HandleCombatMovement(enemy, isReady, abilityToUse);
            }
        }
        else
        {
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
                enemy.currentTarget = null;
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
    private float pathUpdateTimer; // Prevent framerate drops from calculating paths every frame

    public void Enter(EnemyAI enemy)
    {
        enemy.SetAIStatus("Retreating", "Fleeing");
        timer = enemy.retreatDuration;
        pathUpdateTimer = 0f;
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
        pathUpdateTimer -= Time.deltaTime;

        if (timer > 0 && enemy.currentTarget != null)
        {
            if (pathUpdateTimer <= 0f)
            {
                enemy.RetreatFromTarget();
                pathUpdateTimer = 0.5f; // Only run the math twice a second
            }
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
        enemy.NavAgent.stoppingDistance = 0.5f;
        enemy.NavAgent.SetDestination(enemy.StartPosition);
    }

    public void Execute(EnemyAI enemy)
    {
        enemy.currentTarget = enemy.Targeting.FindBestTarget(null);
        if (enemy.currentTarget != null)
        {
            enemy.SwitchState(new CombatState());
            return;
        }

        if (enemy.NavAgent.isOnNavMesh && !enemy.NavAgent.pathPending && enemy.NavAgent.remainingDistance <= 0.5f)
        {
            enemy.NavAgent.ResetPath();
            enemy.SwitchState(new IdleState());
        }
    }

    public void Exit(EnemyAI enemy) { }
}