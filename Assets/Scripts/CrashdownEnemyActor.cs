using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrashdownEnemyActor : MonoBehaviour, IGameActor
{
    public Collider[] myColliders;
    public WeaponDefinition[] weaponsCycle;
    public float aggroRadius = 15.0f;
    public float height = 1.0f;
    public float moveSpeed = 3.0f;
    public bool ignoresTerrain = false;

    public static List<CrashdownEnemyActor> activeEnemies = new List<CrashdownEnemyActor>();

    public enum EAiType
    {
        InanimateObject = 0,
        Stationary = 1,
        RunAtTheKnees = 2,
    }
    public EAiType aiType = EAiType.InanimateObject;

    public Vector3 CurrentFacing { get; set; }

    public enum EAiState
    {
        JustSpawned,
        WalkingAndFighting,
        Dying,
        IsDead,
    }
    public EAiState CurrentAiState { get; set; }
    public IGameActor CurrentAggroTarget { get; set; }
    public float RemainingCooldownTime { get; set; }

    private int currentAttack = 0;

    private void OnEnable()
    {
        activeEnemies.Add(this);
        foreach (Collider collider in myColliders)
        {
            CrashdownGameRoot.actorColliders[collider] = this;
        }
        CurrentAiState = EAiState.JustSpawned;
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
        foreach (Collider collider in myColliders)
        {
            CrashdownGameRoot.actorColliders.Remove(collider);
        }
    }

    public void UpdateFacingAndRenderer()
    {
        transform.LookAt(transform.position + CurrentFacing, Vector3.up);
        // TODO Keep renderer facing camera? Do we need that if we use a sprite renderer?
    }

    public bool CanAttack()
    {
        return RemainingCooldownTime <= 0.0f;
    }

    public bool TryGetCurrentAttack(out WeaponDefinition attack)
    {
        attack = null;
        if (currentAttack >= 0 && currentAttack < weaponsCycle.Length)
        {
            attack = weaponsCycle[currentAttack];
        }
        return attack != null;
    }

    public void AdvanceToNextAttack()
    {
        currentAttack++;
        if (currentAttack >= weaponsCycle.Length)
        {
            currentAttack = 0;
        }
    }

    Vector3 IGameActor.GetFacing()
    {
        return CurrentFacing;
    }

    Vector3 IGameActor.GetPosition()
    {
        return transform.position;
    }

    Quaternion IGameActor.GetRotation()
    {
        return transform.rotation;
    }

    bool IGameActor.IsDodging()
    {
        return false;
    }

    void IGameActor.TakeDamage(float damage, IGameActor attacker)
    {
        // TODO Dying animation.
        // TODO Loot.
        // TODO Switch aggro if the attacker isn't null.
        GameObject.Destroy(gameObject);
    }

}
