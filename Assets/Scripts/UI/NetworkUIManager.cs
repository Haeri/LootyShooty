using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishNet;

public class NetworkUIManager : MonoBehaviour
{
    [SerializeField] public InputField playerNameInput;
    [SerializeField] public InputField ipInput;
    [SerializeField] public InputField portInput;

    [SerializeField] public Button StartServerButton;
    [SerializeField] public Button StartHostButton;
    [SerializeField] public Button JoinButton;

    private void Awake()
    {
        StartServerButton.onClick.AddListener(() =>
        {
            StartServer();
        });
        StartHostButton.onClick.AddListener(() =>
        {
            StartHost();
        });
        JoinButton.onClick.AddListener(() =>
        {
            Join();
        });
    }

    public void StartServer()
    {
        ConnectionManager.Instance.StartupServer(ipInput.text.Trim(), int.Parse(portInput.text.Trim()));        
    }
    public void StartHost()
    {
        ConnectionManager.Instance.StartupHost(ipInput.text.Trim(), int.Parse(portInput.text.Trim()), playerNameInput.text.Trim());        
    }

    public void Join()
    {
        ConnectionManager.Instance.StartupClient(ipInput.text.Trim(), int.Parse(portInput.text.Trim()), playerNameInput.text.Trim());        
    }
}
