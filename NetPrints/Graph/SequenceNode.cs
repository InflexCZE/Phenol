using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Graph
{
    [DataContract]
    public class SequenceNode : Node
    {
        /// <summary>
        /// Input execution pin that executes the loop.
        /// </summary>
        public NodeInputExecPin ExecutionPin
        {
            get => this.InputExecPins[0];
        }

        public NodeOutputExecPin AlwaysPin
        {
            get => this.OutputExecPins[0];
        }
        
        public IEnumerable<NodeOutputExecPin> Branches
        {
            get => this.OutputExecPins.Skip(1);
        }

        public IEnumerable<NodeInputDataPin> Conditions
        {
            get => this.InputDataPins;
        }

        public SequenceNode(NodeGraph graph) :
            base(graph)
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("AfterAll");
            
            AddBranch();
        }

        public void AddBranch()
        {
            int index = this.Branches.Count() + 1;
            AddOutputExecPin($"Branch{index}");
            var conditionPin = AddInputDataPin($"Condition{index}", TypeSpecifier.FromType<bool>());
            conditionPin.UnconnectedValue = true;
        }

        public void RemoveBranch()
        {
            if (this.Branches.LastOrDefault() is { } branchPin)
            {
                var conditionPin = this.Conditions.Last();
                
                GraphUtil.DisconnectOutputExecPin(branchPin);
                GraphUtil.DisconnectInputDataPin(conditionPin);

                this.OutputExecPins.Remove(branchPin);
                this.InputDataPins.Remove(conditionPin);
            }
        }

        public override string ToString()
        {
            return "Sequence";
        }
    }
}
