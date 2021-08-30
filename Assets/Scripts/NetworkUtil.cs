using UnityEngine;
using UnityEngine.SceneManagement;
using MLAPI;
using UnityEngine.UI;

public class NetworkUtil : MonoBehaviour
{
    public InputField playerName;
    public InputField ip;
    public InputField port;

    public void StartClient()
    {
        NetworkSpawner.getInstance().startupClient(ip.text.Trim(), int.Parse(port.text.Trim()), playerName.text.Trim());
    }
    public void StartHost()
    {
        NetworkSpawner.getInstance().startupHost(ip.text.Trim(), int.Parse(port.text.Trim()), playerName.text.Trim());
    }

    public void StartServer()
    {
        NetworkSpawner.getInstance().startupServer(ip.text.Trim(), int.Parse(port.text.Trim()));
    }
}
