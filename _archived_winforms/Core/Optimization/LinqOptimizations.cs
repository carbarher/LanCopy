using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Optimizaciones para queries LINQ comunes
    /// Evita allocations innecesarias y mejora rendimiento
    /// </summary>
    public static class LinqOptimizations
    {
        /// <summary>
        /// FirstOrDefault optimizado para arrays
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FirstOrDefaultFast<T>(this T[] array, Func<T, bool> predicate)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (predicate(array[i]))
                    return array[i];
            }
            return default;
        }
        
        /// <summary>
        /// Any optimizado para arrays
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AnyFast<T>(this T[] array, Func<T, bool> predicate)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (predicate(array[i]))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Count optimizado para arrays
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountFast<T>(this T[] array, Func<T, bool> predicate)
        {
            int count = 0;
            for (int i = 0; i < array.Length; i++)
            {
                if (predicate(array[i]))
                    count++;
            }
            return count;
        }
        
        /// <summary>
        /// Where + ToList optimizado
        /// </summary>
        public static List<T> WhereToListFast<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            var result = new List<T>();
            foreach (var item in source)
            {
                if (predicate(item))
                    result.Add(item);
            }
            return result;
        }
        
        /// <summary>
        /// Select + ToList optimizado
        /// </summary>
        public static List<TResult> SelectToListFast<T, TResult>(
            this IEnumerable<T> source,
            Func<T, TResult> selector)
        {
            var result = new List<TResult>();
            foreach (var item in source)
            {
                result.Add(selector(item));
            }
            return result;
        }
        
        /// <summary>
        /// Distinct optimizado con HashSet
        /// </summary>
        public static List<T> DistinctFast<T>(this IEnumerable<T> source)
        {
            var seen = new HashSet<T>();
            var result = new List<T>();
            
            foreach (var item in source)
            {
                if (seen.Add(item))
                    result.Add(item);
            }
            
            return result;
        }
        
        /// <summary>
        /// GroupBy optimizado
        /// </summary>
        public static Dictionary<TKey, List<T>> GroupByFast<T, TKey>(
            this IEnumerable<T> source,
            Func<T, TKey> keySelector)
        {
            var groups = new Dictionary<TKey, List<T>>();
            
            foreach (var item in source)
            {
                var key = keySelector(item);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<T>();
                    groups[key] = list;
                }
                list.Add(item);
            }
            
            return groups;
        }
        
        /// <summary>
        /// Sum optimizado para arrays
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SumFast(this int[] array)
        {
            long sum = 0;
            for (int i = 0; i < array.Length; i++)
            {
                sum += array[i];
            }
            return sum;
        }
        
        /// <summary>
        /// Max optimizado para arrays
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T MaxFast<T>(this T[] array) where T : IComparable<T>
        {
            if (array.Length == 0)
                throw new InvalidOperationException("Array is empty");
            
            T max = array[0];
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i].CompareTo(max) > 0)
                    max = array[i];
            }
            return max;
        }
        
        /// <summary>
        /// Min optimizado para arrays
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T MinFast<T>(this T[] array) where T : IComparable<T>
        {
            if (array.Length == 0)
                throw new InvalidOperationException("Array is empty");
            
            T min = array[0];
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i].CompareTo(min) < 0)
                    min = array[i];
            }
            return min;
        }
        
        /// <summary>
        /// OrderBy optimizado para listas pequeñas (< 100 items)
        /// </summary>
        public static List<T> OrderByFastSmall<T, TKey>(
            this IEnumerable<T> source,
            Func<T, TKey> keySelector) where TKey : IComparable<TKey>
        {
            var list = source.ToList();
            
            // Insertion sort para listas pequeñas (más rápido que QuickSort)
            for (int i = 1; i < list.Count; i++)
            {
                var key = list[i];
                var keyValue = keySelector(key);
                int j = i - 1;
                
                while (j >= 0 && keySelector(list[j]).CompareTo(keyValue) > 0)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = key;
            }
            
            return list;
        }
    }
}
