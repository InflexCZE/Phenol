using System;
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrintsEditor.Reflection
{
    public class MemoizedReflectionProvider : IReflectionProvider
    {
        private readonly IReflectionProvider provider;

        private Func<TypeSpecifier, IEnumerable<ConstructorSpecifier>> memoizedGetConstructors;
        private Func<TypeSpecifier, IEnumerable<string>> memoizedGetEnumNames;
        private Func<MethodSpecifier, string> memoizedGetMethodDocumentation;
        private Func<MethodSpecifier, int, string> memoizedGetMethodParameterDocumentation;
        private Func<MethodSpecifier, int, string> memoizedGetMethodReturnDocumentation;
        private Func<IEnumerable<TypeSpecifier>> memoizedGetNonStaticTypes;
        private Func<TypeSpecifier, IEnumerable<MethodSpecifier>> memoizedGetOverridableMethodsForType;
        private Func<MethodSpecifier, IEnumerable<MethodSpecifier>> memoizedGetPublicMethodOverloads;
        private Func<TypeSpecifier, TypeSpecifier, bool> memoizedHasImplicitCast;
        private Func<TypeSpecifier, TypeSpecifier, bool> memoizedTypeSpecifierIsSubclassOf;
        private Func<ReflectionProviderMethodQuery, IEnumerable<MethodSpecifier>> memoizedGetMethods;
        private Func<ReflectionProviderVariableQuery, IEnumerable<VariableSpecifier>> memoizedGetVariables;

        public MemoizedReflectionProvider(IReflectionProvider reflectionProvider)
        {
            provider = reflectionProvider;

            Reset();
        }

        /// <summary>
        /// Resets the memoization.
        /// </summary>
        public void Reset()
        {
            memoizedGetConstructors = provider.GetConstructors;
            memoizedGetConstructors = memoizedGetConstructors.Memorize();

            memoizedGetEnumNames = provider.GetEnumNames;
            memoizedGetEnumNames = memoizedGetEnumNames.Memorize();

            memoizedGetMethodDocumentation = provider.GetMethodDocumentation;
            memoizedGetMethodDocumentation = memoizedGetMethodDocumentation.MemorizeValue();

            memoizedGetMethodParameterDocumentation = provider.GetMethodParameterDocumentation;
            memoizedGetMethodParameterDocumentation = memoizedGetMethodParameterDocumentation.MemorizeValue();

            memoizedGetMethodReturnDocumentation = provider.GetMethodReturnDocumentation;
            memoizedGetMethodReturnDocumentation = memoizedGetMethodReturnDocumentation.MemorizeValue();

            memoizedGetNonStaticTypes = provider.GetNonStaticTypes;
            memoizedGetNonStaticTypes = memoizedGetNonStaticTypes.Memorize();

            memoizedGetOverridableMethodsForType = provider.GetOverridableMethodsForType;
            memoizedGetOverridableMethodsForType = memoizedGetOverridableMethodsForType.Memorize();

            memoizedGetMethods = provider.GetMethods;
            memoizedGetMethods = memoizedGetMethods.Memorize();

            memoizedGetPublicMethodOverloads = provider.GetPublicMethodOverloads;
            memoizedGetPublicMethodOverloads = memoizedGetPublicMethodOverloads.Memorize();

            memoizedGetVariables = provider.GetVariables;
            memoizedGetVariables = memoizedGetVariables.Memorize();

            memoizedHasImplicitCast = provider.HasImplicitCast;
            memoizedHasImplicitCast = memoizedHasImplicitCast.MemorizeValue();

            memoizedTypeSpecifierIsSubclassOf = provider.TypeSpecifierIsSubclassOf;
            memoizedTypeSpecifierIsSubclassOf = memoizedTypeSpecifierIsSubclassOf.MemorizeValue();
        }

        public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier)
            => memoizedGetConstructors(typeSpecifier);

        public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier)
            => memoizedGetEnumNames(typeSpecifier);

        public string GetMethodDocumentation(MethodSpecifier methodSpecifier)
            => memoizedGetMethodDocumentation(methodSpecifier);

        public string GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex)
            => memoizedGetMethodParameterDocumentation(methodSpecifier, parameterIndex);

        public string GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex)
            => memoizedGetMethodReturnDocumentation(methodSpecifier, returnIndex);

        public IEnumerable<TypeSpecifier> GetNonStaticTypes()
            => memoizedGetNonStaticTypes();

        public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier)
            => memoizedGetOverridableMethodsForType(typeSpecifier);

        public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier)
            => memoizedGetPublicMethodOverloads(methodSpecifier);

        public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query)
            => memoizedGetMethods(query);

        public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query)
            => memoizedGetVariables(query);

        public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType)
            => memoizedHasImplicitCast(fromType, toType);

        public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b)
            => memoizedTypeSpecifierIsSubclassOf(a, b);
    }
}
