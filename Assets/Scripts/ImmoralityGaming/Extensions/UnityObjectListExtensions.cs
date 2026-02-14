using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public static class UnityObjectListExtensions
{
    public static void DestroyAndClear<T>(this List<T> list, bool destroyGameObject = false) where T : Object
    {
        if (Application.isEditor)
        {
            list.DestroyAndClearInEditor(destroyGameObject);
            return;
        }

        foreach (var item in list.Where(x => x != null))
        {
            Object.Destroy(item);
        }

        list.Clear();
    }

    public static void DeactivateAndClear<T>(this List<T> list) where T : Object
    {
        foreach (var item in list.Where(x => x != null))
        {
            GameObject gameObject = item as GameObject;

            gameObject.SetActive(false);
        }

        list.Clear();
    }

    private static void DestroyAndClearInEditor<T>(this List<T> list, bool destroyGameObject) where T : Object
    {
        foreach (var item in list.Where(x => x != null))
        {
            if (destroyGameObject)
            {
                Object.DestroyImmediate(item.GameObject());
            }
            else
            {
                Object.DestroyImmediate(item);
            }
        }

        list.Clear();
    }
}