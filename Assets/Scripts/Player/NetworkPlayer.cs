using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer Instance { get; private set; }

    public readonly SyncVar<string> playerName = new(string.Empty);
    public readonly SyncVar<int> ping = new(0);

    [SerializeField] public GameObject pawnPrefab;
    
    public GameObject _currentPawn { get; private set; }

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
        _currentPawn = Instantiate(pawnPrefab);
        Spawn(_currentPawn, Owner);
    }
}
