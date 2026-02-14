using System;
using System.Collections;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

public class CoroutineHandler : SingletonBehaviour<CoroutineHandler>
{
    public static void StartActionAfterDelay(Action action, float delay)
    {
        Instance.StartCoroutine(ExecuteAfterTime(action, delay));
    }

    public static void Handle(IEnumerator coroutine)
    {
        Instance.StartRoutine(coroutine);
    }

    private void StartRoutine(IEnumerator coroutine)
    {
        StartCoroutine(coroutine);
    }

    private static IEnumerator ExecuteAfterTime(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action();
    }
}