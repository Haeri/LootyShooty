using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;
using MLAPI.NetworkVariable;
using UnityEngine.UI;

public class NetworkPlayer : NetworkBehaviour
{
    public NetworkVariable<string> playerName;
    public NetworkVariable<int> ping;

    private float nextActionTime = 0.0f;
    private float period = 0.1f;

    private Text networkInfo;

    // Start is called before the first frame update
    void Start()
    {
        if (IsLocalPlayer)
        {
            networkInfo = UIManager.getInstance().networkInfo.GetComponent<Text>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (IsLocalPlayer)
        {
            if (Time.time > nextActionTime)
            {
                nextActionTime += period;
                SendPingRequest();
                networkInfo.text = playerName.Value + "\nPing " + ping.Value;
            }
        }
    }

    private void SendPingRequest()
    {
        PingServerRpc(Time.time);
    }

    [ServerRpc]
    private void PingServerRpc(float PintStart)
    {

        PingClientRpc(PintStart, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        });
    }

    [ClientRpc]
    private void PingClientRpc(float PingStart, ClientRpcParams clientRpcParams = default)
    {
        ping.Value = (int)(Time.time - PingStart);
    }

}
