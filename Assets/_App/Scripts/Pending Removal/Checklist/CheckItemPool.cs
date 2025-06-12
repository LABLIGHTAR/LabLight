using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckItemPool : MonoBehaviour
{
    public static CheckItemPool SharedInstance;
    public List<GameObject> pooledObjects;
    public GameObject objectToPool;
    public int amountToPool;
    public Transform Content;

    void Awake()
    {
        SharedInstance = this;
    }

    public void CreatePooledObjects()
    {
        pooledObjects = new List<GameObject>();
        GameObject tmp;
        for (int i = 0; i < amountToPool; i++)
        {
            tmp = Instantiate(objectToPool, Content);
            tmp.name = "CheckItem" + i;
            tmp.SetActive(false);
            pooledObjects.Add(tmp);
        }
    }

    public GameObject GetPooledObject()
    {
        for (int i = 0; i < amountToPool; i++)
        {
            if (!pooledObjects[i].activeInHierarchy)
            {
                return pooledObjects[i];
            }
        }
        return null;
    }
}

