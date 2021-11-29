using UnityEngine;
using UnityEngine.UI;

public class NetworkUtil : MonoBehaviour
{
    public InputField playerName;
    public InputField ip;
    public InputField port;

    public void StartClient()
    {
        ConnectionManager.Instance.startupClient(ip.text.Trim(), int.Parse(port.text.Trim()), playerName.text.Trim());
    }
    public void StartHost()
    {
        ConnectionManager.Instance.startupHost(ip.text.Trim(), int.Parse(port.text.Trim()), playerName.text.Trim());
    }

    public void StartServer()
    {
        ConnectionManager.Instance.startupServer(ip.text.Trim(), int.Parse(port.text.Trim()));
    }
}
