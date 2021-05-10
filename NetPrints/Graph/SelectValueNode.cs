using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Allows one-of-from list of value selection
    /// First value with true predicate value is selected
    /// </summary>
    [DataContract]
    public class SelectValueNode : ExecNode
    {
        public NodeInputDataPin DefaultValuePin
        {
            get => this.InputDataPins.Last();
        }

        public NodeOutputDataPin OutputValuePin
        {
            get => this.OutputDataPins[0];
        }

        public NodeInputTypePin ValueTypePin
        {
            get => this.InputTypePins[0];
        }

        public NodeOutputExecPin MatchExecPin
        {
            get => this.OutputExecPins[0];
        }

        public NodeOutputExecPin DefaultMatchExecPin
        {
            get => this.OutputExecPins[1];
        }

        public IEnumerable<(NodeInputDataPin Value, NodeInputDataPin Predicate)> Conditionals
        {
            get
            {
                for(var i = 1; i < this.InputDataPins.Count - 1; i+=2)
                {
                    yield return (this.InputDataPins[i], this.InputDataPins[i - 1]);
                }
            }
        }

        public override bool CanSetPure => true;

        public SelectValueNode(NodeGraph graph) :
            base(graph)
        {
            AddInputTypePin("ValueType");
            AddOutputDataPin("Value", TypeSpecifier.FromType<object>());
            AddInputDataPin("Default", TypeSpecifier.FromType<object>());
            RegisterEvents();

            AddConditional();
        }

        protected override void AddExecPins()
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("Match");
            AddOutputExecPin("DefaultMatch");
        }

        public override void OnMethodDeserialized()
        {
            base.OnMethodDeserialized();
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            this.DefaultValuePin.IncomingPinChanged += OnDefaultValueChanged;
            UpdateNodeType();
        }

        private void OnDefaultValueChanged(NodeInputDataPin pin, NodeOutputDataPin oldPin, NodeOutputDataPin newPin)
        {
            UpdateNodeType();
        }
        
        protected override void OnInputTypeChanged(object sender, EventArgs eventArgs)
        {
            base.OnInputTypeChanged(sender, eventArgs);
            UpdateNodeType();
        }

        private void UpdateNodeType()
        {
            var valueType = this.ValueTypePin.InferredType ??
                             this.DefaultValuePin.IncomingPin?.PinType ??
                             TypeSpecifier.FromType<object>();

            if(this.OutputValuePin.PinType.Value == valueType)
                return;

            var dataPins = new NodeDataPin[] { this.OutputValuePin, this.DefaultValuePin }
                            .Concat(this.Conditionals.Select(x => x.Value))
                            .ToArray();

            foreach(var dataPin in dataPins)
            {
                GraphUtil.DisconnectPin(dataPin);
            }

            foreach(var dataPin in dataPins)
            {
                dataPin.PinType.Value = valueType;
            }
        }

        public void AddConditional()
        {
            int conditionals = this.Conditionals.Count();
            var pinType = this.OutputValuePin.PinType.Value;
            var insertIndex = conditionals * 2;
            var index = conditionals + 1;
            
            AddInputDataPin($"Condition{index}", TypeSpecifier.FromType<bool>(), insertIndex);
            AddInputDataPin($"Value{index}", pinType, insertIndex + 1);
        }

        public void RemoveConditional()
        {
            var (predicate, value) = this.Conditionals.LastOrDefault();
            if(predicate is null)
                return;

            GraphUtil.DisconnectPin(value);
            GraphUtil.DisconnectPin(predicate);
            
            this.InputDataPins.Remove(value);
            this.InputDataPins.Remove(predicate);
        }

        public override string ToString()
        {
            return "Select";
        }
    }
}
