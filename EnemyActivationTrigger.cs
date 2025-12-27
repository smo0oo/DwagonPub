using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemyActivationTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // CASE 1: Trigger is on the Player (or a generic zone), detecting Enemies entering.
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            enemy.ActivateAI();
            return;
        }

        // CASE 2: Trigger is on the Enemy (Aggro Range), detecting the Player entering.
        // We check if the THING that entered is the player.
        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            // If the player entered, wake up the enemy that OWNS this trigger.
            EnemyAI self = GetComponentInParent<EnemyAI>();
            if (self != null)
            {
                self.ActivateAI();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Same logic for Deactivation
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            enemy.DeactivateAI();
            return;
        }

        if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
        {
            EnemyAI self = GetComponentInParent<EnemyAI>();
            if (self != null)
            {
                self.DeactivateAI();
            }
        }
    }
}