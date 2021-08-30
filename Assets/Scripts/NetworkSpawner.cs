using UnityEngine;
using UnityEngine.SceneManagement;
using MLAPI;
using MLAPI.Transports.UNET;

public class NetworkSpawner : NetworkBehaviour
{
    public string defaultIP = "127.0.0.1";
    public int defaultPort = 7777;
    public GameObject playerPrefab;
    public GameObject spectatorCamera;



    private static NetworkSpawner _instance;

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

    public static NetworkSpawner getInstance()
    {
        return _instance;
    }

    private void spawnPlayer(ulong clientID, string name)
    {
        GameObject go = Instantiate(playerPrefab, new Vector3(0, 5, 0), Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientID);
        go.GetComponent<NetworkPlayer>().playerName.Value = name;
        Debug.Log("Player Spawned " + clientID);
    }

    public void startupClient()
    {
        startupClient(defaultIP, defaultPort);
    }
    public void startupClient(string ip, int port, string name = "")
    {
        SceneManager.LoadSceneAsync("TestScene").completed += (op) =>
        {
            NetworkManager.Singleton.NetworkConfig.CreatePlayerPrefab = false;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort = port;
            NetworkManager.Singleton.StartClient();

            if(name == "")
            {
                name = "Player_" + NetworkManager.Singleton.LocalClientId;
            }
            spawnPlayer(NetworkManager.Singleton.LocalClientId, name);
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
            NetworkManager.Singleton.NetworkConfig.CreatePlayerPrefab = false;
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
            //Instantiate(spectatorCamera);
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ServerListenPort = port;
            NetworkManager.Singleton.StartServer();
        };
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
