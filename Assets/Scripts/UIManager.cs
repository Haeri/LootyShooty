using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] public GameObject itemTextPanel;
    [SerializeField] public GameObject hitmarker;
    [SerializeField] public GameObject damagemarker;
    [SerializeField] public GameObject networkInfo;

    private static UIManager _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    public static UIManager getInstance()
    {
        return _instance;
    }

    public void disconnect()
    {
        NetworkSpawner.getInstance().Disconnect();
    }

}
