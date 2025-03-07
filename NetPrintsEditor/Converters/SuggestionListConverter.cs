﻿using NetPrints.Core;
using NetPrints.Graph;
using NetPrintsEditor.Controls;
using NetPrintsEditor.Dialogs;
using NetPrintsEditor.ViewModels;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace NetPrintsEditor.Converters
{
    public class SuggestionListConverter : IValueConverter
    {
        private readonly MethodSpecifierConverter methodSpecifierConverter = new MethodSpecifierConverter();

        // See https://docs.microsoft.com/en-us/dotnet/framework/wpf/app-development/pack-uris-in-wpf for format
        private static readonly string IconPathPrefix = $"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/";

        public object Convert(object tupleObject, Type targetType, object parameter, CultureInfo culture)
        {
            string text;
            string iconPath;

            var item = (SearchableComboBoxItem)tupleObject;
            var value = item.Value;

            if (value is MethodSpecifier methodSpecifier)
            {
                text = methodSpecifierConverter.Convert(methodSpecifier, typeof(string), parameter, culture) as string;

                iconPath = OperatorUtil.IsOperator(methodSpecifier) ? "Operator_16x.png" : "Method_16x.png";
            }
            else if (value is VariableSpecifier variableSpecifier)
            {
                text = $"{variableSpecifier.Type} {variableSpecifier.Name} : {variableSpecifier.Type}";
                iconPath = (variableSpecifier.Modifiers & VariableModifiers.Event) != 0 ? "Event_16x.png" : "Property_16x.png";
            }
            else if (value is MakeDelegateTypeInfo makeDelegateTypeInfo)
            {
                if(makeDelegateTypeInfo.TargetType is {} delegateTargetType)
                {
                    text = $"Make Delegate For A Method Of {delegateTargetType.ShortName}";
                }
                else if (makeDelegateTypeInfo.DelegateType is { } delegateType)
                {
                    text = $"Make Delegate of type {delegateType.ShortName}";
                }
                else
                {
                    text = "Make Delegate";
                }

                iconPath = "Delegate_16x.png";
            }
            else if (value is TypeSpecifier t)
            {
                if (t == TypeSpecifier.FromType<ForLoopNode>())
                {
                    text = "For Loop";
                    iconPath = "Loop_16x.png";
                }
                else if (t == TypeSpecifier.FromType<ForeachLoopNode>())
                {
                    text = "Foreach Loop";
                    iconPath = "Loop_16x.png";
                }
                else if (t == TypeSpecifier.FromType<SelectValueNode>())
                {
                    text = "Select Value";
                    iconPath = "LocalVariable_16x.png";
                }
                else if (t == TypeSpecifier.FromType<SequenceNode>())
                {
                    text = "Sequence";
                    iconPath = "CheckboxList_16x.png";
                }
                else if (t == TypeSpecifier.FromType<DelayNode>())
                {
                    text = "Delay";
                    iconPath = "Time_16x.png";
                }
                else if (t == TypeSpecifier.FromType<AttributesNode>())
                {
                    text = "Attributes";
                    iconPath = "Schema_16x.png";
                }
                else if (t == TypeSpecifier.FromType<RerouteNode>())
                {
                    text = "Pin";
                    iconPath = "PinnedItem_16x.png";
                }
                else if (t == TypeSpecifier.FromType<IfElseNode>())
                {
                    text = "If Else";
                    iconPath = "If_16x.png";
                }
                else if (t == TypeSpecifier.FromType<ConstructorNode>())
                {
                    text = "Construct New Object";
                    iconPath = "Create_16x.png";
                }
                else if (t == TypeSpecifier.FromType<TypeOfNode>())
                {
                    text = "Type Of";
                    iconPath = "Type_16x.png";
                }
                else if (t == TypeSpecifier.FromType<ExplicitCastNode>())
                {
                    text = "Explicit Cast";
                    iconPath = "Convert_16x.png";
                }
                else if (t == TypeSpecifier.FromType<ReturnNode>())
                {
                    text = "Return";
                    iconPath = "Return_16x.png";
                }
                else if (t == TypeSpecifier.FromType<MakeArrayNode>())
                {
                    text = "Make Array";
                    iconPath = "ListView_16x.png";
                }
                else if (t == TypeSpecifier.FromType<LiteralNode>())
                {
                    text = "Literal";
                    iconPath = "Literal_16x.png";
                }
                else if (t == TypeSpecifier.FromType<TypeNode>())
                {
                    text = "Type";
                    iconPath = "Type_16x.png";
                }
                else if (t == TypeSpecifier.FromType<MakeArrayTypeNode>())
                {
                    text = "Make Array Type";
                    iconPath = "Type_16x.png";
                }
                else if (t == TypeSpecifier.FromType<ThrowNode>())
                {
                    text = "Throw";
                    iconPath = "Throw_16x.png";
                }
                else if (t == TypeSpecifier.FromType<AwaitNode>())
                {
                    text = "Await";
                    iconPath = "Task_16x.png";
                }
                else if (t == TypeSpecifier.FromType<TernaryNode>())
                {
                    text = "Ternary";
                    iconPath = "ConditionalRule_16x.png";
                }
                else if (t == TypeSpecifier.FromType<DefaultNode>())
                {
                    text = "Default";
                    iconPath = "None_16x.png";
                }
                else
                {
                    text = t.FullCodeName;
                    iconPath = "Type_16x.png";
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            if (targetType == typeof(string))
            {
                return text;
            }
            else
            {
                var fullIconPath = IconPathPrefix + iconPath;;
                return new SuggestionListItemBinding(text, fullIconPath);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
