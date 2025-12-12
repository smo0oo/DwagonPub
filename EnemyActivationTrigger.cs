using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemyActivationTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // When a collider enters our trigger, try to get its EnemyAI component.
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            // If we found one, tell it to wake up.
            enemy.ActivateAI();
        }
    }

    void OnTriggerExit(Collider other)
    {
        // When a collider leaves our trigger, try to get its EnemyAI component.
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            // If we found one, tell it to go to sleep.
            enemy.DeactivateAI();
        }
    }
}