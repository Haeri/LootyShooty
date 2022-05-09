using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using System.Collections.Generic;
using System.Text;
using FishNet;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    public string defaultIP = "127.0.0.1";
    public int defaultPort = 7777;

    private void Awake()
    {
        Instance = this;
    }

    public void StartupClient()
    {
        StartupClient(defaultIP, defaultPort);
    }
    public void StartupClient(string ip, int port, string name = "")
    {
        InstanceFinder.TransportManager.Transport.SetServerBindAddress(ip, FishNet.Transporting.IPAddressType.IPv4);
        InstanceFinder.TransportManager.Transport.SetPort((ushort)port);
        InstanceFinder.ClientManager.StartConnection();
    }
    
    public void StartupHost()
    {
        StartupHost(defaultIP, defaultPort);
    }
    public void StartupHost(string ip, int port, string name = "")
    {
        InstanceFinder.TransportManager.Transport.SetServerBindAddress(ip, FishNet.Transporting.IPAddressType.IPv4);
        InstanceFinder.TransportManager.Transport.SetPort((ushort)port);
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();
    }

    public void StartupServer()
    {
        StartupServer(defaultIP, defaultPort);
    }
    public void StartupServer(string ip, int port)
    {
        InstanceFinder.TransportManager.Transport.SetServerBindAddress(ip, FishNet.Transporting.IPAddressType.IPv4);
        InstanceFinder.TransportManager.Transport.SetPort((ushort)port);
        InstanceFinder.ServerManager.StartConnection();
    }

    public void Disconnect()
    {
        
    }
}
