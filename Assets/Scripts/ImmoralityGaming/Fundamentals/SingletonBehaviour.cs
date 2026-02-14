using UnityEngine;

namespace ImmoralityGaming.Fundamentals
{
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        public bool dontDestroyOnLoad = false;
        
        public bool DetachFromRoot = false;

        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<T>();
                    if (instance == null)
                    {
                        var obj = new GameObject
                        {
                            name = typeof(T).Name
                        };
                        instance = obj.AddComponent<T>();
                    }
                }

                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;

                if (dontDestroyOnLoad)
                {
                    if (DetachFromRoot)
                    {
                        this.transform.parent = null;
                        DontDestroyOnLoad(this);
                    }
                    else
                    {
                        DontDestroyOnLoad(this.transform.root.gameObject);
                    }
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
    }
}