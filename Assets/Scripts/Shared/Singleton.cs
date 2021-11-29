using UnityEngine;

public class Singleton<T> : MonoBehaviour
    where T : Component
{
    private static T _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this.GetComponent<T>();
        }
    }

    public static T Instance
    {
        get
        {          
            return _instance;
        }
    }
}