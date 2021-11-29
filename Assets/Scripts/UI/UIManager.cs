using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    [SerializeField] public GameObject itemTextPanel;
    [SerializeField] public GameObject hitmarker;
    [SerializeField] public GameObject damagemarker;
    [SerializeField] public GameObject networkInfo;
    [SerializeField] public GameObject killFeed;


    public void disconnect()
    {
        ConnectionManager.Instance.Disconnect();
    }

}
