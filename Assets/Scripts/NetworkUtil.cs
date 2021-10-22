using UnityEngine;
using UnityEngine.UI;

public class NetworkUtil : MonoBehaviour
{
    public InputField playerName;
    public InputField ip;
    public InputField port;

    public void StartClient()
    {
        ConnectionManager.getInstance().startupClient(ip.text.Trim(), int.Parse(port.text.Trim()), playerName.text.Trim());
    }
    public void StartHost()
    {
        ConnectionManager.getInstance().startupHost(ip.text.Trim(), int.Parse(port.text.Trim()), playerName.text.Trim());
    }

    public void StartServer()
    {
        ConnectionManager.getInstance().startupServer(ip.text.Trim(), int.Parse(port.text.Trim()));
    }
}
