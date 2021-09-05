using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;

public class NetworkPhysicsItem : NetworkBehaviour
{
    public String itemName;

    void Start()
    {
        if (IsClient && !IsServer)
        {
            // Remove Rigidbody, since this is a newtwork object and
            // the server is going to take care of physics simulation
            Destroy(GetComponent<Rigidbody>());
        }
    }

}
