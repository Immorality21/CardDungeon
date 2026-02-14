using System;
using System.Collections.Generic;

namespace ImmoralityGaming.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();

            foreach (var element in source)
            {
                if (keySelector != null && seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
		
		public static TSource MinBy<TSource>(this IEnumerable<TSource> source, Func<TSource, IComparable> projectionToComparable)
        {
            using (var e = source.GetEnumerator())
            {
                if (!e.MoveNext())
                {
                    throw new InvalidOperationException("Sequence is empty.");
                }
                TSource min = e.Current;
                IComparable minProjection = projectionToComparable(e.Current);
                while (e.MoveNext())
                {
                    IComparable currentProjection = projectionToComparable(e.Current);
                    if (currentProjection.CompareTo(minProjection) < 0)
                    {
                        min = e.Current;
                        minProjection = currentProjection;
                    }
                }
                return min;
            }
        }

        public static TSource MaxBy<TSource>(this IEnumerable<TSource> source, Func<TSource, IComparable> projectionToComparable)
        {
            using (var e = source.GetEnumerator())
            {
                if (!e.MoveNext())
                {
                    throw new InvalidOperationException("Sequence is empty.");
                }
                TSource max = e.Current;
                IComparable maxProjection = projectionToComparable(e.Current);
                while (e.MoveNext())
                {
                    IComparable currentProjection = projectionToComparable(e.Current);
                    if (currentProjection.CompareTo(maxProjection) > 0)
                    {
                        max = e.Current;
                        maxProjection = currentProjection;
                    }
                }
                return max;
            }
        }

        public static string ToCommaSeperatedString(this IEnumerable<string> source, string seperator = ", ")
        {
            return string.Join(seperator, source);
        }
    }
}
