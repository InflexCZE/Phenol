using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    public enum MethodParameterPassType
    {
        Default,
        Reference,
        Out,
        In
    }

    [DataContract]
    public class MethodParameter : Named<BaseType>
    {
        [DataMember]
        public MethodParameterPassType PassType
        {
            get;
            private set;
        }

        /// <summary>
        /// Whether the parameter has an explicit default value.
        /// </summary>
        [DataMember]
        public bool HasExplicitDefaultValue
        {
            get;
            private set;
        }

        /// <summary>
        /// Explicit default value for the parameter.
        /// Only valid when HasExplicitDefaultValue is true.
        /// </summary>
        [DataMember]
        public object ExplicitDefaultValue
        {
            get;
            private set;
        }

        public MethodParameter(string name, BaseType type, MethodParameterPassType passType,
            bool hasExplicitDefaultValue, object explicitDefaultValue)
            : base(name, type)
        {
            PassType = passType;
            HasExplicitDefaultValue = hasExplicitDefaultValue;
            ExplicitDefaultValue = explicitDefaultValue;
        }
    }

    /// <summary>
    /// Specifier describing a method.
    /// </summary>
    [Serializable]
    [DataContract]
    public class MethodSpecifier
    {
        /// <summary>
        /// Name of the method without any prefixes.
        /// </summary>
        [DataMember]
        public string Name { get; private set; }

        /// <summary>
        /// Specifier for the type this method is contained in.
        /// </summary>
        [DataMember]
        public TypeSpecifier DeclaringType { get; private set; }

        /// <summary>
        /// Named specifiers for the types this method takes as arguments.
        /// </summary>
        [DataMember]
        public IList<MethodParameter> Parameters { get; private set; }

        /// <summary>
        /// Specifiers for the types this method takes as arguments.
        /// </summary>
        public IReadOnlyList<BaseType> ArgumentTypes => this.ArgumentTypesFast.ToArray();
        
        private IEnumerable<BaseType> ArgumentTypesFast => this.Parameters.Select(t => (BaseType)t);

        /// <summary>
        /// Specifiers for the types this method returns.
        /// </summary>
        [DataMember]
        public IList<BaseType> ReturnTypes { get; private set; }

        /// <summary>
        /// Modifiers this method has.
        /// </summary>
        [DataMember]
        public MethodModifiers Modifiers { get; private set; }

        /// <summary>
        /// Visibility of this method.
        /// </summary>
        [DataMember]
        public MemberVisibility Visibility { get; private set; }

        /// <summary>
        /// Generic arguments this method takes.
        /// </summary>
        [DataMember]
        public IList<BaseType> GenericArguments { get; private set; }

        private readonly int HashCodeCache;

        /// <summary>
        /// Creates a MethodSpecifier.
        /// </summary>
        /// <param name="name">Name of the method without any prefixes.</param>
        /// <param name="arguments">Specifiers for the arguments of the method.</param>
        /// <param name="returnTypes">Specifiers for the return types of the method.</param>
        /// <param name="modifiers">Modifiers of the method.</param>
        /// <param name="declaringType">Specifier for the type this method is contained in.</param>
        /// <param name="genericArguments">Generic arguments this method takes.</param>
        public MethodSpecifier
        (
            string name, 
            IEnumerable<MethodParameter> arguments,
            IEnumerable<BaseType> returnTypes, 
            MethodModifiers modifiers, 
            MemberVisibility visibility, 
            TypeSpecifier declaringType,
            IEnumerable<BaseType> genericArguments
        )
        {
            this.Name = name;
            this.DeclaringType = declaringType;
            this.Parameters = arguments.ToList();
            this.ReturnTypes = returnTypes.ToList();
            this.Modifiers = modifiers;
            this.Visibility = visibility;
            this.GenericArguments = genericArguments.ToList();

            this.HashCodeCache = HashCode.Combine
            (
                this.Name, 
                this.Modifiers, 
                string.Join(",", this.GenericArguments), 
                string.Join(",", this.ReturnTypes), 
                string.Join(",", this.Parameters), 
                this.Visibility, 
                this.DeclaringType
            );
        }

        public override string ToString()
        {
            string methodString = "";

            if (Modifiers.HasFlag(MethodModifiers.Static))
            {
                methodString += $"{DeclaringType.ShortName}.";
            }

            methodString += Name;

            string argTypeString = string.Join(", ", Parameters.Select(a => a.Value.ShortName));

            methodString += $"({argTypeString})";

            if (GenericArguments.Count > 0)
            {
                string genArgTypeString = string.Join(", ", GenericArguments.Select(s => s.ShortName));
                methodString += $"<{genArgTypeString}>";
            }

            if (ReturnTypes.Count > 0)
            {
                string returnTypeString = string.Join(", ", ReturnTypes.Select(s => s.ShortName));
                methodString += $" : {returnTypeString}";
            }

            return methodString;
        }

        public override bool Equals(object obj)
        {
            if (obj is MethodSpecifier methodSpec)
            {
                return
                    methodSpec.Modifiers == Modifiers
                    && methodSpec.Parameters.Count == Parameters.Count
                    && methodSpec.Name == Name
                    && methodSpec.DeclaringType == DeclaringType
                    && methodSpec.ReturnTypes.SequenceEqual(ReturnTypes)
                    && methodSpec.GenericArguments.SequenceEqual(GenericArguments)

                    //TODO: This doesn't seem right. Hash code is calculated based on full parameters while Equality is based only on parameters
                    && methodSpec.ArgumentTypesFast.SequenceEqual(ArgumentTypesFast);
            }

            return base.Equals(obj);
        }
        
        public override int GetHashCode()
        {
            return this.HashCodeCache;
        }

        public static bool operator==(MethodSpecifier a, MethodSpecifier b)
        {
            if (a is null)
            {
                return b is null;
            }

            return a.Equals(b);
        }

        public static bool operator !=(MethodSpecifier a, MethodSpecifier b)
        {
            if (a is null)
            {
                return !(b is null);
            }

            return !a.Equals(b);
        }
    }
}
