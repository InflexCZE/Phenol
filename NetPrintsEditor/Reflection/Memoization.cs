using System;
using System.Linq;
using System.Collections.Generic;

namespace NetPrintsEditor.Reflection
{
    public static class Memoization
    {
        // https://stackoverflow.com/a/2852595/4332314

        public static Func<IEnumerable<R>> Memorize<R>(this Func<IEnumerable<R>> f)
        {
            IEnumerable<R> r = null;
            return () => r ??= f().ToArray();
        }
        
        public static Func<A, R> MemorizeValue<A, R>(this Func<A, R> f)
        {
            var d = new Dictionary<A, R>();

            return a =>
            {
                if (!d.TryGetValue(a, out var r))
                {
                    r = f(a);
                    d.Add(a, r);
                }

                return r;
            };
        }

        public static Func<A, IEnumerable<R>> Memorize<A, R>(this Func<A, IEnumerable<R>> f)
        {
            return ((Func<A, IEnumerable<R>>)(a => f(a).ToArray())).MemorizeValue();
        }

        public static Func<A, B, R> MemorizeValue<A, B, R>(this Func<A, B, R> f)
        {
            return f.Tuplify().MemorizeValue().Detuplify();
        }
        
        public static Func<A, B, IEnumerable<R>> Memorize<A, B, R>(this Func<A, B, IEnumerable<R>> f)
        {
            return f.Tuplify().Memorize().Detuplify();
        }

        public static Func<ValueTuple<A, B>, R> Tuplify<A, B, R>(this Func<A, B, R> f)
        {
            return t => f(t.Item1, t.Item2);
        }

        public static Func<A, B, R> Detuplify<A, B, R>(this Func<ValueTuple<A, B>, R> f)
        {
            return (a, b) => f((a, b));
        }
    }
}
