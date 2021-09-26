using UnityEngine;
using UnityEngine.SceneManagement;
using MLAPI;
using MLAPI.Transports.UNET;
using MLAPI.Messaging;
using System.Collections.Generic;
using System.Text;

public class ConnectionManager : NetworkBehaviour
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

    private static ConnectionManager _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            _instance = this;
        }
    }
    public static ConnectionManager getInstance()
    {
        return _instance;
    }


    private void Start()
    {
        //NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += handleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        NetworkManager.Singleton.NetworkConfig.CreatePlayerPrefab = false;
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
    }

    private void spawnPlayer(ulong clientID, string name)
    {
        GameObject go = Instantiate(playerPrefab, new Vector3(0, 5, 0), Quaternion.identity);
        go.name = name;
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientID);
        go.GetComponent<NetworkPlayer>().playerName.Value = name;
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
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ServerListenPort = port;
            NetworkManager.Singleton.StartHost();
           
            if (name == "")
            {
                name = "Host_" + NetworkManager.Singleton.LocalClientId;
            }
            spawnPlayer(NetworkManager.Singleton.LocalClientId, name);
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


    private void handleClientConnected(ulong clientId)
    {

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
        callback(false, null, true, null, null);
        spawnPlayer(clientId, connectionPayload.name);
    }

    public void Disconnect()
    {
        if (IsHost)
        {
            NetworkManager.Singleton.StopHost();
        }
        else if (IsClient)
        {
            NetworkManager.Singleton.StopClient();
        }
        else if (IsServer)
        {
            NetworkManager.Singleton.StopServer();
        }

        SceneManager.LoadScene("MainMenu");

    }
}
