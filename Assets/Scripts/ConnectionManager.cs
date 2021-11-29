using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode.Transports.UNET;

public class ConnectionManager : NetworkSingleton<ConnectionManager>
{
    public string defaultIP = "127.0.0.1";
    public int defaultPort = 7777;
    public GameObject playerPrefab;
    public GameObject spectatorCamera;

    public struct ConnectionPayload
    {
        public string name;
    }

    public struct PlayerData
    {
        public string name;
    }

    private Dictionary<ulong, PlayerData> clientData;


    private void Start()
    {
        //NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
        //NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
    }

    private void spawnPlayer(ulong clientID, string name)
    {
        GameObject go = Instantiate(playerPrefab, new Vector3(0, 5, 0), Quaternion.identity);
        go.name = name;
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientID);
        go.GetComponent<NetworkPlayer>().playerName.Value = "Player_"+name;
        Debug.Log($"Spawned Player {name} ID:[{clientID}]");
    }

    public void startupClient()
    {
        startupClient(defaultIP, defaultPort);
    }
    public void startupClient(string ip, int port, string name = "")
    {
        SceneManager.LoadSceneAsync("TestScene").completed += (op) =>
        {
            var payload = JsonUtility.ToJson(new ConnectionPayload()
            {
                name = name
            });

            byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);

            NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort = port;
            NetworkManager.Singleton.StartClient();
        };
    }
    
    public void startupHost()
    {
        startupHost(defaultIP, defaultPort);
    }
    public void startupHost(string ip, int port, string name = "")
    {
        SceneManager.LoadSceneAsync("TestScene").completed += (op) =>
        {
            var payload = JsonUtility.ToJson(new ConnectionPayload()
            {
                name = name
            });

            byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);

            NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ServerListenPort = port;
            NetworkManager.Singleton.StartHost();
           
            /*
            if (name == "")
            {
                name = "Host_" + NetworkManager.Singleton.LocalClientId;
            }
            spawnPlayer(NetworkManager.Singleton.LocalClientId, name);
           */
        };
    }

    public void startupServer()
    {
        startupServer(defaultIP, defaultPort);
    }
    public void startupServer(string ip, int port)
    {
        SceneManager.LoadSceneAsync("TestScene").completed += (op) =>
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ServerListenPort = port;
            NetworkManager.Singleton.StartServer();
        };
    }


    private void HandleClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            //spawnPlayer(clientId, "PETER");
            /*
            return;
            SetSeedClientRPC(Random.seed, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });
            */
        }
    }

    [ClientRpc]
    private void SetSeedClientRPC(int seed, ClientRpcParams clientRpcParams = default)
    {
        Random.InitState(seed);
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (IsServer)
        {
            
            foreach (ulong key in NetworkManager.Singleton.ConnectedClients.Keys)
            {
                Debug.Log("Dict: "+  key);
            }
            Debug.Log("client: " + clientId);
            //NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponent<PlayerController>().DropItem();
        }
    }

    private void ApprovalCheck(byte[] connectionData, ulong clientId, NetworkManager.ConnectionApprovedDelegate callback)
    {
        string payload = Encoding.ASCII.GetString(connectionData);
        var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload);      
        //spawnPlayer(clientId, connectionPayload.name);
        callback(true, null, true, null, null);
    }

    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();      
        SceneManager.LoadScene("MainMenu");
    }
}
