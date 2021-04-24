using System;
using System.Collections.Generic;
using NetPrints.Graph;

namespace NetPrints.Core
{
    public static class CoroutineUtils
    {
        public const string CoroutineReturnArgName = "___return___";
        public const string CoroutineEnumeratorName = "Coroutine";
        public const string CoroutineReturnValueField = "Value";

        public static TypeSpecifier GetCoroutineReturnType(IEnumerable<BaseType> types)
        {
            return new("Ref", false, false, new[]
            {
                GenericsHelper.BuildAggregateType(types)
            });
        }
    }
}
