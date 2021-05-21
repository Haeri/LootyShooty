using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.NetworkVariable;
using System;

public class Damagable : NetworkBehaviour
{
    public int maxHealth = 100;
    public NetworkVariable<int> health = new NetworkVariable<int>(100);

    public GameObject hitEffect;
    public bool isPenetrable;

    public event Action<int> OnDamage;
    public event Action OnDeath;

    public bool TakeDamage(int amount)
    {
        health.Value -= amount;
        OnDamage?.Invoke(amount);
        
        if(health.Value < 0)
        {
            health.Value = 0;
            OnDeath?.Invoke();
            return true;
        }

        return false;
    }

    public void ResetHealth()
    {
        health.Value = maxHealth;   
    }

}
