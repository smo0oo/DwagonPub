using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemyActivationTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            enemy.ActivateAI();
            return;
        }

        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            EnemyAI self = GetComponentInParent<EnemyAI>();
            if (self != null)
            {
                self.ActivateAI();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            // Don't deactivate an enemy if they are currently fighting!
            if (enemy.currentTarget != null) return;

            enemy.DeactivateAI();
            return;
        }

        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            EnemyAI self = GetComponentInParent<EnemyAI>();
            if (self != null)
            {
                // FIX: If the enemy is chasing the player (has a target), ignore the exit trigger.
                // The enemy's own "Chase Leash Radius" will handle resetting if they go too far.
                if (self.currentTarget != null) return;

                self.DeactivateAI();
            }
        }
    }
}