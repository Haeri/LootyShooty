using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelRefs : MonoBehaviour
{ 
    public static LevelRefs Instance { get; private set; }

    [SerializeField] public GameObject LevelCamera;

    private void Awake()
    {
        Instance = this;
    }
}
