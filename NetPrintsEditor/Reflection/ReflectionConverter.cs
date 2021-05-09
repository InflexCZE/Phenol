using Microsoft.CodeAnalysis;
using NetPrints.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetPrintsEditor.Reflection
{
    /// <summary>
    /// Helper class for converting from Roslyn symbols to NetPrints specifiers.
    /// </summary>
    public static class ReflectionConverter
    {
        public static MemberVisibility VisibilityFromAccessibility(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Private   => MemberVisibility.Private,
                Accessibility.Protected => MemberVisibility.Protected,
                Accessibility.Public    => MemberVisibility.Public,
                Accessibility.Internal  => MemberVisibility.Internal,
                
                // TODO: Do this correctly (eg. internal protected, private protected etc.)
                // https://stackoverflow.com/a/585869/4332314
                _ => MemberVisibility.Public
            };
        }

        public static TypeSpecifier TypeSpecifierFromSymbol(ITypeSymbol type)
        {
            string typeName;

            if (type is IArrayTypeSymbol)
            {
                // TODO: Get more interesting type?
                typeName = typeof(Array).FullName;
            }
            else
            {
                // Get the nested name (represented by + between classes)
                // See https://stackoverflow.com/questions/2443244/having-a-in-the-class-name
                string nestedPrefix = "";
                ITypeSymbol containingType = type.ContainingType;
                while (containingType != null)
                {
                    nestedPrefix = $"{containingType.Name}+{nestedPrefix}";
                    containingType = containingType.ContainingType;
                }

                typeName = nestedPrefix + type.Name.Split('`').First();
                if (type.ContainingNamespace != null && !type.ContainingNamespace.IsGlobalNamespace)
                {
                    typeName = type.ContainingNamespace + "." + typeName;
                }
            }

            var typeSpecifier = new TypeSpecifier
            (
                    typeName,
                    type.TypeKind == TypeKind.Enum,
                    type.TypeKind == TypeKind.Interface,
                    type.TypeKind == TypeKind.Delegate
            );

            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsUnboundGenericType)
                {
                    throw new ArgumentException(nameof(type));
                }

                foreach (ITypeSymbol genType in namedType.TypeArguments)
                {
                    if (genType is ITypeParameterSymbol genTypeParam)
                    {
                        // TODO: Convert and add constraints
                        typeSpecifier.GenericArguments.Add(GenericTypeSpecifierFromSymbol(genTypeParam));
                    }
                    else
                    {
                        typeSpecifier.GenericArguments.Add(TypeSpecifierFromSymbol(genType));
                    }
                }
            }

            return typeSpecifier;
        }

        public static GenericType GenericTypeSpecifierFromSymbol(ITypeParameterSymbol type)
        {
            // TODO: Convert constraints
            GenericType genericType = new GenericType(type.Name);

            return genericType;
        }

        public static BaseType BaseTypeSpecifierFromSymbol(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol typeParam)
            {
                return GenericTypeSpecifierFromSymbol(typeParam);
            }
            else
            {
                return TypeSpecifierFromSymbol(type);
            }
        }

        public static Named<BaseType> NamedBaseTypeSpecifierFromSymbol(IParameterSymbol paramSymbol)
        {
            return new Named<BaseType>(paramSymbol.Name, BaseTypeSpecifierFromSymbol(paramSymbol.Type));
        }

        private static readonly Dictionary<RefKind, MethodParameterPassType> refKindToPassType = new Dictionary<RefKind, MethodParameterPassType>()
        {
            [RefKind.None] = MethodParameterPassType.Default,
            [RefKind.Ref] = MethodParameterPassType.Reference,
            [RefKind.Out] = MethodParameterPassType.Out,
            [RefKind.In] = MethodParameterPassType.In,
        };

        public static MethodParameter MethodParameterFromSymbol(IParameterSymbol paramSymbol)
        {
            return new MethodParameter(paramSymbol.Name, BaseTypeSpecifierFromSymbol(paramSymbol.Type), refKindToPassType[paramSymbol.RefKind],
                paramSymbol.HasExplicitDefaultValue, paramSymbol.HasExplicitDefaultValue ? paramSymbol.ExplicitDefaultValue : null);
        }

        public static MethodSpecifier MethodSpecifierFromSymbol(IMethodSymbol method)
        {
            MemberVisibility visibility = VisibilityFromAccessibility(method.DeclaredAccessibility);

            var modifiers = MethodModifiers.None;

            if (method.IsVirtual)
            {
                modifiers |= MethodModifiers.Virtual;
            }

            if (method.IsSealed)
            {
                modifiers |= MethodModifiers.Sealed;
            }

            if (method.IsAbstract)
            {
                modifiers |= MethodModifiers.Abstract;
            }

            if (method.IsStatic)
            {
                modifiers |= MethodModifiers.Static;
            }

            if (method.IsOverride)
            {
                modifiers |= MethodModifiers.Override;
            }

            if (method.IsAsync)
            {
                modifiers |= MethodModifiers.Async;
            }

            var returnTypes = method.ReturnsVoid ?
                Array.Empty<BaseType>() :
                new[] { BaseTypeSpecifierFromSymbol(method.ReturnType) };

            var parameters = method.Parameters.Select(p => MethodParameterFromSymbol(p));
            var genericArgs = method.TypeParameters.Select(p => BaseTypeSpecifierFromSymbol(p));

            return new MethodSpecifier
            (
                method.Name,
                parameters,
                returnTypes,
                modifiers,
                visibility,
                TypeSpecifierFromSymbol(method.ContainingType),
                genericArgs
            );
        }

        public static VariableSpecifier VariableSpecifierFromSymbol(IPropertySymbol property)
        {
            var getterAccessibility = property.GetMethod?.DeclaredAccessibility;
            var setterAccessibility = property.SetMethod?.DeclaredAccessibility;

            var modifiers = new VariableModifiers();

            if (property.IsStatic)
            {
                modifiers |= VariableModifiers.Static;
            }

            if (property.IsReadOnly)
            {
                modifiers |= VariableModifiers.ReadOnly;
            }

            // TODO: More modifiers

            return new VariableSpecifier(
                property.Name,
                TypeSpecifierFromSymbol(property.Type),
                getterAccessibility.HasValue ? VisibilityFromAccessibility(getterAccessibility.Value) : MemberVisibility.Private,
                setterAccessibility.HasValue ? VisibilityFromAccessibility(setterAccessibility.Value) : MemberVisibility.Private,
                TypeSpecifierFromSymbol(property.ContainingType),
                modifiers);
        }

        public static VariableSpecifier VariableSpecifierFromEvent(IEventSymbol @event)
        {
            var visibility = VisibilityFromAccessibility(@event.DeclaredAccessibility);

            var modifiers = VariableModifiers.Event;

            if (@event.IsStatic)
            {
                modifiers |= VariableModifiers.Static;
            }

            // TODO: More modifiers

            return new VariableSpecifier
            (
                @event.Name,
                TypeSpecifierFromSymbol(@event.Type),
                visibility,
                visibility,
                TypeSpecifierFromSymbol(@event.ContainingType),
                modifiers
            );
        }

        public static VariableSpecifier VariableSpecifierFromField(IFieldSymbol field, List<VariableSpecifier> syntheticSymbols)
        {
            var visibility = VisibilityFromAccessibility(field.DeclaredAccessibility);

            var modifiers = new VariableModifiers();

            if (field.IsStatic)
            {
                modifiers |= VariableModifiers.Static;
            }

            if (field.IsConst)
            {
                modifiers |= VariableModifiers.Const;
            }

            if (field.IsReadOnly)
            {
                modifiers |= VariableModifiers.ReadOnly;
            }

            // TODO: More modifiers
            
            var fieldType = TypeSpecifierFromSymbol(field.Type);
            var containingType = TypeSpecifierFromSymbol(field.ContainingType);
            
            if(fieldType.IsDelegate && field.AssociatedSymbol is null)
            {
                //This is delegate field but not defined as full event
                //=> Emit synthetic symbol to allow (un)subscription
                syntheticSymbols.Add(new VariableSpecifier
                (
                    field.Name,
                    fieldType,
                    visibility,
                    visibility,
                    containingType,
                    modifiers | VariableModifiers.Event
                ));
            }

            return new VariableSpecifier
            (
                field.Name,
                fieldType,
                visibility,
                visibility,
                containingType,
                modifiers
            );
        }

        public static ConstructorSpecifier ConstructorSpecifierFromSymbol(IMethodSymbol constructorMethodSymbol)
        {
            return new ConstructorSpecifier(
                constructorMethodSymbol.Parameters.Select(p => MethodParameterFromSymbol(p)),
                TypeSpecifierFromSymbol(constructorMethodSymbol.ContainingType));
        }
    }
}
