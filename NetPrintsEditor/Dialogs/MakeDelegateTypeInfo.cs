using NetPrints.Core;

namespace NetPrintsEditor.Dialogs
{
    public class MakeDelegateTypeInfo
    {
        public TypeSpecifier FromType { get; }
        public TypeSpecifier TargetType { get; }
        public TypeSpecifier DelegateType { get; }

        public MakeDelegateTypeInfo(TypeSpecifier targetType, TypeSpecifier delegateType, TypeSpecifier fromType)
        {
            this.FromType = fromType;
            this.TargetType = targetType;
            this.DelegateType = delegateType;
        }
    }
}
