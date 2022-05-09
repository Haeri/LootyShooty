using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer Instance { get; private set; }

    [SyncVar] public string playerName;
    [SyncVar] public int ping;

    [SerializeField] public GameObject pawnPrefab;
    [SerializeField] public GameObject currentPawn;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner) return;

        Instance = this;
    }

    public void SpawnPawn()
    {            
        ServerSpawnPawn();
    }

    [ServerRpc]
    private void ServerSpawnPawn()
    {
        currentPawn = Instantiate(pawnPrefab);
        Spawn(currentPawn, Owner);
    }
}