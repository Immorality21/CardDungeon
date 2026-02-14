using System.Collections.Generic;
using UnityEngine;

namespace ImmoralityGaming.Fundamentals
{
    public class ObjectPooler : MonoBehaviour
    {
        public GameObject pooledObject;
        public int pooledAmount = 20;
        public bool canGrow = true;
        public bool Uiobj = false;
        public Transform objParent;
        
        private List<GameObject> pooledObjectsList;
        private bool isInitialized = false;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            pooledObjectsList = new List<GameObject>();
            for (int i = 0; i < pooledAmount; i++)
            {
                var obj = InstantiateNewObject();
                obj.SetActive(false);
            }
            isInitialized = true;
        }

        public GameObject GetPooledObject()
        {
            if (!isInitialized)
            {
                Initialize();
            }

            for (int i = 0; i < pooledObjectsList.Count; i++)
            {
                if (!pooledObjectsList[i].activeInHierarchy)
                {
                    return pooledObjectsList[i];
                }
            }
            if (canGrow)
            {
                return InstantiateNewObject();
            }
            return null;
        }

        private GameObject InstantiateNewObject()
        {
            var obj = Instantiate(pooledObject);
            pooledObjectsList.Add(obj);
            if (objParent)
            {
                if (Uiobj)
                {
                    obj.transform.SetParent(objParent, false);
                }
                else
                {
                    obj.transform.SetParent(objParent, true);
                }
            }
            return obj;
        }
    }
}
