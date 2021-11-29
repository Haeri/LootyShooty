using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class ObjectPool : Singleton<ObjectPool>
{
    [System.Serializable]
    public struct PoolObject
    {
        public GameObject prefab;
        public int maxLength;
    }

    struct Ringbuffer
    {
        public List<GameObject> list;
        public int currentIndex;
    }

    public List<PoolObject> poolPrefabs;

    private Dictionary<GameObject, Ringbuffer> ringBufferMap;


    void Start()
    {
        ringBufferMap = new Dictionary<GameObject, Ringbuffer>();

        foreach (PoolObject item in poolPrefabs)
        {
            GameObject folder = new GameObject(item.prefab.name);
            folder.transform.SetParent(transform);


            Ringbuffer rb = new Ringbuffer();
            rb.list = new List<GameObject>(item.maxLength);
            rb.currentIndex = 0;

            for ( int i = 0; i < item.maxLength; i++)
            {
                GameObject go = Instantiate(item.prefab) as GameObject;
                go.transform.SetParent(folder.transform);
                go.SetActive(false);
                rb.list.Add(go);
            }

            ringBufferMap.Add(item.prefab, rb);
        }
    }

    public GameObject instanciate(GameObject prefab)
    {
        Ringbuffer rb = ringBufferMap[prefab];
        
        GameObject obj = resetObject(rb.list[rb.currentIndex]);
        rb.currentIndex = (rb.currentIndex + 1) % rb.list.Count;
        ringBufferMap[prefab] = rb;

        return obj;
    }

    public GameObject resetObject(GameObject obj) 
    {
        obj.SetActive(false);
        obj.transform.position = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        obj.transform.SetParent(null);

        IPoolInstanceResetter pir = obj.GetComponent<IPoolInstanceResetter>();
        if(pir != null)
        {
            pir.reset();
        }

        obj.SetActive(true);

        return obj;
    }
}
