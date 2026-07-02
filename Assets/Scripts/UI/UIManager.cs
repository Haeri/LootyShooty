using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishNet;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] public GameObject itemTextPanel;
    [SerializeField] public GameObject hitmarker;
    [SerializeField] public GameObject damagemarker;
    [SerializeField] public GameObject networkInfo;
    [SerializeField] public GameObject killFeed;


    [SerializeField] public Button disconnectButton;
    [SerializeField] public Button spawnButton;

    private void Awake()
    {
        Instance = this;

        disconnectButton.onClick.AddListener(() =>
        {
            InstanceFinder.ClientManager.StopConnection();
            InstanceFinder.ServerManager.StopConnection(true);
        });

        spawnButton.onClick.AddListener(() =>
        {
            // Instance only exists on a client; a server-only session has no local player to spawn.
            if (NetworkPlayer.Instance == null)
            {
                Debug.LogWarning("Cannot spawn a pawn without a local player. Start as host or join as client first.");
                return;
            }

            NetworkPlayer.Instance.SpawnPawn();
        });
    }
}
