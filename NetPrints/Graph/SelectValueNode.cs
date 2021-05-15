using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            get
            {
                Debug.Assert(this.UseFlowInputs == false);
                return this.InputDataPins.Last();
            }
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
            get
            {
                Debug.Assert(this.UseFlowInputs == false);
                return this.OutputExecPins[1];
            }
        }

        public bool UseFlowInputs
        {
            get => this.useFlowInputs;

            set
            {
                if(value != this.useFlowInputs)
                {
                    SwitchNodeType();
                }
            }
        }

        [DataMember]
        private bool useFlowInputs;
        
        public IEnumerable<(NodeInputDataPin Value, NodeInputDataPin Predicate)> Conditionals
        {
            get
            {
                Debug.Assert(this.UseFlowInputs == false);

                for(var i = 1; i < this.InputDataPins.Count - 1; i+=2)
                {
                    yield return (this.InputDataPins[i], this.InputDataPins[i - 1]);
                }
            }
        }

        public IEnumerable<(NodeInputDataPin Value, NodeInputExecPin Flow)> InputFlows
        {
            get
            {
                Debug.Assert(this.UseFlowInputs);

                for (var i = 0; i < this.InputExecPins.Count; i++)
                {
                    yield return (this.InputDataPins[i], this.InputExecPins[i]);
                }
            }
        }

        public override bool CanSetPure => this.UseFlowInputs == false;

        public SelectValueNode(NodeGraph graph) :
            base(graph)
        {
            AddInputTypePin("ValueType");
            AddOutputDataPin("Value", TypeSpecifier.FromType<object>());
            AddDefaultValue();

            AddBranch();
        }

        protected override void AddExecPins()
        {
            if(this.UseFlowInputs)
            {
                AddOutputExecPin("Exec");
            }
            else
            {
                AddInputExecPin("Exec");
                AddOutputExecPin("Match");
                AddOutputExecPin("DefaultMatch");
            }
        }

        public override void OnMethodDeserialized()
        {
            base.OnMethodDeserialized();
            RegisterEvents();
        }

        private void AddDefaultValue()
        {
            AddInputDataPin("Default", this.OutputValuePin.PinType.Value);
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            if(this.UseFlowInputs)
            {
                foreach(var (value, _) in this.InputFlows)
                {
                    value.IncomingPinChanged += OnInputValueChanged;
                }
            }
            else
            {
                foreach (var (value, _) in this.Conditionals)
                {
                    value.IncomingPinChanged += OnInputValueChanged;
                }

                this.DefaultValuePin.IncomingPinChanged += OnInputValueChanged;
            }
            
            UpdateNodeType();
        }

        private void OnInputValueChanged(NodeInputDataPin pin, NodeOutputDataPin oldPin, NodeOutputDataPin newPin)
        {
            UpdateNodeType();
        }
        
        protected override void OnInputTypeChanged(object sender, EventArgs eventArgs)
        {
            base.OnInputTypeChanged(sender, eventArgs);
            UpdateNodeType();
        }

        private bool UpdatingNodeType;
        private void UpdateNodeType()
        {
            if(this.UpdatingNodeType)
                return;

            this.UpdatingNodeType = true;
            try
            {
                NodeInputDataPin dontDisconnect = null;
                var valueType = this.ValueTypePin.InferredType;

                if(valueType is null)
                {
                    if(this.UseFlowInputs == false)
                    {
                        valueType = this.DefaultValuePin.IncomingPin?.PinType;
                    }
                }
                
                IEnumerable<NodeInputDataPin> dataPins = Array.Empty<NodeInputDataPin>();

                if(this.UseFlowInputs)
                {
                    dataPins = dataPins.Concat(this.InputFlows.Select(x => x.Value));
                }
                else
                {
                    dataPins = dataPins.Append(this.DefaultValuePin);
                    dataPins = dataPins.Concat(this.Conditionals.Select(x => x.Value));
                }

                if(valueType is null)
                {
                    foreach(var value in dataPins)
                    {
                        if(value.IncomingPin?.PinType.Value is { } pinType)
                        {
                            dontDisconnect = value;
                            valueType = pinType;
                            break;
                        }
                    }
                }

                valueType ??= TypeSpecifier.FromType<object>();

                if(this.OutputValuePin.PinType.Value != valueType)
                {
                    GraphUtil.DisconnectPin(this.OutputValuePin);
                    this.OutputValuePin.PinType.Value = valueType;
                }


                foreach(var dataPin in dataPins)
                {
                    if(dataPin.PinType.Value != valueType)
                    {
                        if(ReferenceEquals(dataPin, dontDisconnect) == false)
                        {
                            GraphUtil.DisconnectPin(dataPin);
                        }
                        
                        dataPin.PinType.Value = valueType;
                    }
                }
            }
            finally
            {
                this.UpdatingNodeType = false;
            }
        }

        public (NodeInputDataPin Value, NodePin ConditionalOrFlow) AddBranch()
        {
            int displayIndex;
            int valueInsertIndex;
            NodePin conditionalOrFlow;
            
            if (this.UseFlowInputs)
            {
                int inputFlows = this.InputFlows.Count();
                valueInsertIndex = inputFlows;
                displayIndex = inputFlows + 1;
                
                conditionalOrFlow = AddInputExecPin($"Flow{displayIndex}");
            }
            else
            {
                int conditionals = this.Conditionals.Count();
                var insertIndex = conditionals * 2;
                displayIndex = conditionals + 1;
                valueInsertIndex = insertIndex + 1;
                
                conditionalOrFlow = AddInputDataPin($"Condition{displayIndex}", TypeSpecifier.FromType<bool>(), insertIndex);
            }
            
            var pinType = this.OutputValuePin.PinType.Value;
            var value = AddInputDataPin($"Value{displayIndex}", pinType, valueInsertIndex);
            value.IncomingPinChanged += OnInputValueChanged;

            return (value, conditionalOrFlow);
        }

        public void RemoveBranch()
        {
            NodePin value;
            NodePin predicate;
            if(this.UseFlowInputs)
            {
                (predicate, value) = this.InputFlows.LastOrDefault();
            }
            else
            {
                (predicate, value) = this.Conditionals.LastOrDefault();
            }

            if(predicate is not null)
            {
                RemovePin(value);
                RemovePin(predicate);
            }
        }

        private void SwitchNodeType()
        {
            NodeInputExecPin outFlow = null;
            if(this.IsPure == false)
            {
                outFlow = this.MatchExecPin.OutgoingPin;
            }
            
            var inputValues = new List<(NodeOutputDataPin, object)>();

            if(this.UseFlowInputs)
            {
                foreach (var (value, branch) in this.InputFlows.ToArray())
                {
                    inputValues.Add((value.IncomingPin, value.UnconnectedValue));

                    RemovePin(value);
                    RemovePin(branch);
                }
            }
            else
            {
                foreach(var (value, branch) in this.Conditionals.ToArray())
                {
                    inputValues.Add((value.IncomingPin, value.UnconnectedValue));

                    RemovePin(value);
                    RemovePin(branch);
                }
                
                RemovePin(this.DefaultValuePin);
            }
            
            RemoveExecPins();

            this.useFlowInputs = !this.useFlowInputs;

            AddExecPins();
            if(outFlow is not null)
            {
                GraphUtil.ConnectExecPins(this.MatchExecPin, outFlow);
            }

            foreach(var (inputPin, inputValue) in inputValues)
            {
                var (newValue, _) = AddBranch();
                newValue.UnconnectedValue = inputValue;

                if(inputPin is not null)
                {
                    GraphUtil.ConnectDataPins(inputPin, newValue);
                }
            }

            if(this.UseFlowInputs == false)
            {
                AddDefaultValue();
            }
        }

        public override string ToString()
        {
            return "Select";
        }
    }
}
