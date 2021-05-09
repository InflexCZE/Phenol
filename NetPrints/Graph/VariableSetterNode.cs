using NetPrints.Core;
using System.Runtime.Serialization;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node that sets the value of a variable.
    /// </summary>
    [DataContract]
    public class VariableSetterNode : VariableNode
    {
        /// <summary>
        /// Input data pin for the new value of the variable.
        /// </summary>
        public NodeInputDataPin NewValuePin
        {
            get { return IsStatic ? InputDataPins[0] : InputDataPins[1]; }
        }

        public NodeInputDataPin SubscribePin
        {
            get { return IsStatic ? InputDataPins[1] : InputDataPins[2]; }
        }

        public VariableSetterNode(NodeGraph graph, VariableSpecifier variable)
            : base(graph, variable)
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("Exec");

            AddInputDataPin("NewValue", variable.Type);

            if(this.IsEvent)
            {
                AddInputDataPin("Subscribe", TypeSpecifier.FromType<bool>());
            }
        }

        public override string ToString()
        {
            string staticText = IsStatic ? $"{TargetType.ShortName}." : "";
            return $"Set {staticText}{VariableName}";
        }
    }
}
