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

    private void spawnPlayer(ulong clientID)
    {
        GameObject go = Instantiate(playerPrefab, new Vector3(0, 5, 0), Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientID);
        Debug.Log("Player Spawned " + clientID);
    }

    public void startupClient()
    {
        startupClient(defaultIP, defaultPort);
    }
    public void startupClient(string ip, int port)
    {
        SceneManager.LoadSceneAsync("TestScene").completed += (op) =>
        {
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort = port;
            NetworkManager.Singleton.StartClient();
        };
    }
    
    public void startupHost()
    {
        startupHost(defaultIP, defaultPort);
    }
    public void startupHost(string ip, int port)
    {
        SceneManager.LoadSceneAsync("TestScene").completed += (op) =>
        {
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = ip;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ServerListenPort = port;
            NetworkManager.Singleton.StartHost();
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
            Instantiate(spectatorCamera);
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
