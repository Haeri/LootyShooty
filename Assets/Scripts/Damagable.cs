using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

public class Damagable : NetworkBehaviour
{
    public int maxHealth = 100;
    [SyncVar]
    public int health = 100;

    public GameObject hitEffect;
    public bool isPenetrable;

    public event Action<int> OnDamage;
    public event Action OnDeath;

    public bool IsDead()
    {
        return health <= 0;
    }

    public bool TakeDamage(int amount)
    {
        if (health == 0)
        {
            return false;
        }

        health -= amount;
        OnDamage?.Invoke(amount);
        
        if(health < 0)
        {
            health = 0;
            OnDeath?.Invoke();
            return true;
        }

        return false;
    }

    public void ResetHealth()
    {
        health = maxHealth;   
    }

}
