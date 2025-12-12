using UnityEngine;
using System.Collections.Generic;

public class DomeAbilityHolder : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("An optional transform to specify where projectiles should spawn from. If not set, the dome's pivot point is used.")]
    public Transform projectileSpawnPoint;

    private Dictionary<Ability, float> cooldowns = new Dictionary<Ability, float>();

    // --- NEW: Buffers for Non-Allocating Physics and lists ---
    private Collider[] _aoeBuffer = new Collider[100];
    private List<CharacterRoot> _affectedCharactersBuffer = new List<CharacterRoot>(100);

    public bool CanUseAbility(Ability ability, GameObject target)
    {
        if (ability == null) return false;
        if (cooldowns.ContainsKey(ability) && Time.time < cooldowns[ability]) return false;
        if (ability.abilityType == AbilityType.TargetedProjectile && target == null) return false;
        return true;
    }

    public void UseAbility(Ability ability, GameObject target)
    {
        UseAbility(ability, target, false);
    }

    public void UseAbility(Ability ability, GameObject target, bool bypassCooldown)
    {
        if (!CanUseAbility(ability, target)) return;

        PayCostAndStartCooldown(ability, bypassCooldown);
        if (ability.castVFX != null) ObjectPooler.instance.Get(ability.castVFX, transform.position, transform.rotation);

        switch (ability.abilityType)
        {
            case AbilityType.TargetedProjectile:
            case AbilityType.ForwardProjectile:
                HandleProjectile(ability, target);
                break;
            case AbilityType.Self:
                HandleSelfCast(ability);
                break;
        }
    }

    public void UseAbility(Ability ability, Vector3 position)
    {
        UseAbility(ability, position, false);
    }

    public void UseAbility(Ability ability, Vector3 position, bool bypassCooldown)
    {
        if (!CanUseAbility(ability, null)) return;

        PayCostAndStartCooldown(ability, bypassCooldown);
        if (ability.castVFX != null) ObjectPooler.instance.Get(ability.castVFX, transform.position, transform.rotation);

        switch (ability.abilityType)
        {
            case AbilityType.GroundAOE:
                HandleGroundAOE(ability, position);
                break;
            case AbilityType.GroundPlacement:
                HandleGroundPlacement(ability, position);
                break;
        }
    }

    private void PayCostAndStartCooldown(Ability ability, bool bypassCooldown = false)
    {
        if (!bypassCooldown)
        {
            cooldowns[ability] = Time.time + ability.cooldown;
        }
    }

    private void HandleGroundPlacement(Ability ability, Vector3 position)
    {
        if (ability.placementPrefab != null)
        {
            GameObject trapObject = Instantiate(ability.placementPrefab, position, Quaternion.identity);
            if (trapObject.TryGetComponent<PlaceableTrap>(out var trap))
            {
                trap.owner = this.gameObject;
            }
        }
    }

    private void HandleGroundAOE(Ability ability, Vector3 position)
    {
        if (ability.hitVFX != null) ObjectPooler.instance.Get(ability.hitVFX, position, Quaternion.identity);

        // --- MODIFIED: Use Non-Allocating version and reusable list ---
        int hitCount = Physics.OverlapSphereNonAlloc(position, ability.aoeRadius, _aoeBuffer);
        _affectedCharactersBuffer.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _aoeBuffer[i];
            CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();
            if (hitCharacter == null || _affectedCharactersBuffer.Contains(hitCharacter))
            {
                continue;
            }
            _affectedCharactersBuffer.Add(hitCharacter);

            bool isHostile = hitCharacter.gameObject.layer != this.gameObject.layer;
            var effectsToApply = isHostile ? ability.hostileEffects : ability.friendlyEffects;

            foreach (var effect in effectsToApply)
            {
                effect.Apply(this.gameObject, hitCharacter.gameObject);
            }
        }
    }

    private void HandleProjectile(Ability ability, GameObject target)
    {
        if (ability.playerProjectilePrefab == null) return;

        Transform spawnTransform = projectileSpawnPoint != null ? projectileSpawnPoint : this.transform;
        Vector3 spawnPos = spawnTransform.position;
        Quaternion spawnRot = spawnTransform.rotation;

        if (target != null)
        {
            CharacterRoot targetRoot = target.GetComponentInParent<CharacterRoot>();
            if (targetRoot != null)
            {
                Collider targetCollider = targetRoot.GetComponentInChildren<Collider>();
                if (targetCollider != null)
                {
                    Vector3 targetPosition = targetCollider.bounds.center;
                    Vector3 direction = targetPosition - spawnPos;
                    if (direction != Vector3.zero)
                    {
                        spawnRot = Quaternion.LookRotation(direction);
                    }
                }
            }
        }

        GameObject projectileGO = ObjectPooler.instance.Get(ability.playerProjectilePrefab, spawnPos, spawnRot);
        if (projectileGO == null) return;

        projectileGO.layer = LayerMask.NameToLayer("FriendlyRanged");

        Collider projectileCollider = projectileGO.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            Collider[] casterColliders = this.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in casterColliders)
            {
                Physics.IgnoreCollision(projectileCollider, c);
            }
        }

        if (projectileGO.TryGetComponent<Projectile>(out var projectile))
        {
            projectile.Initialize(ability, this.gameObject, this.gameObject.layer);
        }
    }

    private void HandleSelfCast(Ability ability)
    {
        if (ability.aoeRadius <= 0)
        {
            foreach (var effect in ability.friendlyEffects)
            {
                effect.Apply(this.gameObject, this.gameObject);
            }
            return;
        }

        // --- MODIFIED: Use Non-Allocating version and reusable list ---
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, ability.aoeRadius, _aoeBuffer);
        _affectedCharactersBuffer.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _aoeBuffer[i];
            CharacterRoot hitCharacter = hit.GetComponentInParent<CharacterRoot>();
            if (hitCharacter == null || _affectedCharactersBuffer.Contains(hitCharacter))
            {
                continue;
            }

            if (hitCharacter.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                _affectedCharactersBuffer.Add(hitCharacter);
                foreach (var effect in ability.friendlyEffects)
                {
                    effect.Apply(this.gameObject, hitCharacter.gameObject);
                }
            }
        }
    }
}