using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Sandbox
{
    /// <summary>A scene-independent host for the sandbox's coroutines.</summary>
    public class SandboxRunner : MonoBehaviour
    {
        private static SandboxRunner _instance;

        public static void Run(IEnumerator routine)
        {
            if (_instance == null)
            {
                var go = new GameObject("SandboxRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SandboxRunner>();
            }
            _instance.StartCoroutine(routine);
        }
    }
}
