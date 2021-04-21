using System;
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

        private static readonly string[] LineSeparator = { Environment.NewLine };
        public static IEnumerable<string> SplitByLines(this string str)
        {
            return str.Split(LineSeparator, StringSplitOptions.None);

        }
    }
}