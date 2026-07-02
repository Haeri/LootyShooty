using UnityEngine;
using FishNet.Object;

public class NetworkItem : NetworkBehaviour
{
    public string itemName;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        GetComponent<Rigidbody>().isKinematic = !IsServerInitialized;
    }
}
