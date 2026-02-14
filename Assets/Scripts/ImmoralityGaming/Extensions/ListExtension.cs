using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmoralityGaming.Extensions
{
    public static class ListExtension
    {
        public static T TakeRandom<T>(this List<T> list)
        {
            if (list.Count == 0)
            {
                return default;
            }

            var random = UnityEngine.Random.Range(0, list.Count);
            return list[random];
        }

        public static T TakeRandom<T>(this List<T> list, Func<T, bool> predicate)
        {
            if (!list.Any(predicate))
            {
                return default;
            }

            var random = UnityEngine.Random.Range(0, list.Count(predicate) - 1);
            return list.Where(predicate).ToList()[random];
        }

        public static List<T> Shuffle<T>(this List<T> list)
        {
            var rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }

            return list;
        }

        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }

        public static void AddDistinct<T>(this List<T> list, T newItem) where T : class
        {
            if (list.Contains(newItem))
            {
                return;
            }

            list.Add(newItem);
        }
    }
}
