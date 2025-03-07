﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetPrints.Utils
{
    public static class Extensions
    {
        public static int IndexOf<T>(this IEnumerable<T> source, T value)
        {
            var comparer = EqualityComparer<T>.Default;

            int index = 0;
            foreach (var item in source)
            {
                if (comparer.Equals(item, value))
                    return index;

                index++;
            }

            return -1;
        }

        public static void Add<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, KeyValuePair<TKey, TValue> pair)
        {
            dictionary.Add(pair.Key, pair.Value);
        }
        
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        private static readonly string[] LineSeparator = { Environment.NewLine };
        public static IEnumerable<string> SplitByLines(this string str)
        {
            return str.Split(LineSeparator, StringSplitOptions.None);
        }

        public static IEnumerable<(T1, T2)> Zip<T1, T2>(this IEnumerable<T1> a, IEnumerable<T2> b)
        {
            // ReSharper disable once ConvertClosureToMethodGroup
            return a.Zip(b, (t1, t2) => ValueTuple.Create(t1, t2));
        }
    }
}