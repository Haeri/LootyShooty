using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

public class NetworkItem : NetworkBehaviour
{
    public String itemName;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        GetComponent<Rigidbody>().isKinematic = !IsServer;       
    }
}
