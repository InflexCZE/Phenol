using NetPrints.Core;
using PropertyChanged;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract base class for all node types.
    /// </summary>
    [DataContract]
    [KnownType(typeof(CallMethodNode))]
    [KnownType(typeof(MethodEntryNode))]
    [KnownType(typeof(ConstructorEntryNode))]
    [KnownType(typeof(ForLoopNode))]
    [KnownType(typeof(ForeachLoopNode))]
    [KnownType(typeof(DelayNode))]
    [KnownType(typeof(IfElseNode))]
    [KnownType(typeof(LiteralNode))]
    [KnownType(typeof(ReturnNode))]
    [KnownType(typeof(ClassReturnNode))]
    [KnownType(typeof(VariableGetterNode))]
    [KnownType(typeof(VariableSetterNode))]
    [KnownType(typeof(ConstructorNode))]
    [KnownType(typeof(MakeDelegateNode))]
    [KnownType(typeof(TypeOfNode))]
    [KnownType(typeof(ExplicitCastNode))]
    [KnownType(typeof(RerouteNode))]
    [KnownType(typeof(MakeArrayNode))]
    [KnownType(typeof(TypeNode))]
    [KnownType(typeof(MakeArrayTypeNode))]
    [KnownType(typeof(ThrowNode))]
    [KnownType(typeof(AwaitNode))]
    [KnownType(typeof(TernaryNode))]
    [KnownType(typeof(TypeReturnNode))]
    [KnownType(typeof(DefaultNode))]
    [KnownType(typeof(AttributesNode))]
    [KnownType(typeof(SequenceNode))]
    [KnownType(typeof(SelectValueNode))]
    [AddINotifyPropertyChangedInterface]
    public abstract class Node
    {
        /// <summary>
        /// Input data pins of this node.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeInputDataPin> InputDataPins { get; private set; } = new ObservableRangeCollection<NodeInputDataPin>();

        /// <summary>
        /// Output data pins of this node.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeOutputDataPin> OutputDataPins { get; private set; } = new ObservableRangeCollection<NodeOutputDataPin>();

        /// <summary>
        /// Input execution pins of this node.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeInputExecPin> InputExecPins { get; private set; } = new ObservableRangeCollection<NodeInputExecPin>();

        /// <summary>
        /// Output execution pins of this node.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeOutputExecPin> OutputExecPins { get; private set; } = new ObservableRangeCollection<NodeOutputExecPin>();

        /// <summary>
        /// Input type pins of this node.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeInputTypePin> InputTypePins { get; private set; } = new ObservableRangeCollection<NodeInputTypePin>();

        /// <summary>
        /// Output type pins of this node.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<NodeOutputTypePin> OutputTypePins { get; private set; } = new ObservableRangeCollection<NodeOutputTypePin>();

        /// <summary>
        /// Delegate for the event of a position change of a node.
        /// </summary>
        /// <param name="node">Node that changed position.</param>
        /// <param name="positionX">New position x value.</param>
        /// <param name="positionY">New position y value.</param>
        public delegate void NodePositionChangedDelegate(Node node, double positionX, double positionY);

        /// <summary>
        /// Called when this node's position changes.
        /// </summary>
        public event NodePositionChangedDelegate OnPositionChanged;

        /// <summary>
        /// Visual position x of this node.
        /// Triggers a call to OnPositionChange when set.
        /// </summary>
        [DataMember]
        public double PositionX
        {
            get => positionX;
            set
            {
                positionX = value;
                OnPositionChanged?.Invoke(this, positionX, positionY);
            }
        }

        /// <summary>
        /// Visual position y of this node.
        /// Triggers a call to OnPositionChange when set.
        /// </summary>
        [DataMember]
        public double PositionY
        {
            get => positionY;
            set
            {
                positionY = value;
                OnPositionChanged?.Invoke(this, positionX, positionY);
            }
        }

        private double positionX;
        private double positionY;

        /// <summary>
        /// Name of this node.
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// Whether this is a pure node (ie. one without any execution pins).
        /// These nodes will usually be executed when one of their output data
        /// pins is used in an execution node.
        /// </summary>
        public bool IsPure
        {
            get
            {
                return InputExecPins.Count == 0 && OutputExecPins.Count == 0;
            }
            set
            {
                if (!CanSetPure)
                {
                    throw new InvalidOperationException("Can't set purity of this node.");
                }

                if (IsPure != value)
                {
                    SetPurity(value);
                }

                Debug.Assert(value == IsPure, "Purity could not be set correctly.");
            }
        }

        public virtual bool CanSetPure
        {
            get => false;
        }

        protected virtual void SetPurity(bool pure)
        {
        }

        /// <summary>
        /// Method graph this node is contained in.
        /// Null if the graph is not a MethodGraph.
        /// </summary>
        public MethodGraph MethodGraph
        {
            get => Graph as MethodGraph;
        }

        /// <summary>
        /// Graph this node is contained in.
        /// </summary>
        [DataMember]
        public NodeGraph Graph
        {
            get;
            private set;
        }

        protected Node(NodeGraph graph)
        {
            Graph = graph;
            Graph.Nodes.Add(this);

            Name = NetPrintsUtil.GetUniqueName(GetType().Name, Graph.Nodes.Select(n => n.Name).ToList());
        }

        public override string ToString()
        {
            return GraphUtil.SplitCamelCase(GetType().Name);
        }

        /// <summary>
        /// Adds an input data pin to this node.
        /// </summary>
        /// <param name="pinName">Name of the pin.</param>
        /// <param name="pinType">Specifier for the type of this pin.</param>
        /// <param name="index">Insertion index</param>
        protected NodeInputDataPin AddInputDataPin(string pinName, ObservableValue<BaseType> pinType, int? index = null)
        {
            var pin = new NodeInputDataPin(this, pinName, pinType);
            this.InputDataPins.Insert(index ?? this.InputDataPins.Count, pin);
            return pin;
        }

        /// <summary>
        /// Adds an output data pin to this node.
        /// </summary>
        /// <param name="pinName">Name of the pin.</param>
        /// <param name="pinType">Specifier for the type of this pin.</param>
        /// <param name="index">Insertion index</param>
        protected NodeOutputDataPin AddOutputDataPin(string pinName, ObservableValue<BaseType> pinType, int? index = null)
        {
            var pin = new NodeOutputDataPin(this, pinName, pinType);
            this.OutputDataPins.Insert(index ?? this.OutputDataPins.Count, pin);
            return pin;
        }

        /// <summary>
        /// Adds an input execution pin to this node.
        /// </summary>
        /// <param name="pinName">Name of the pin.</param>
        /// <param name="index">Insertion index</param>
        protected NodeInputExecPin AddInputExecPin(string pinName, int? index = null)
        {
            var pin = new NodeInputExecPin(this, pinName);
            this.InputExecPins.Insert(index ?? this.InputExecPins.Count, pin);
            return pin;
        }

        /// <summary>
        /// Adds an output execution pin to this node.
        /// </summary>
        /// <param name="pinName">Name of the pin.</param>
        /// <param name="index">Insertion index</param>
        protected NodeOutputExecPin AddOutputExecPin(string pinName, int? index = null)
        {
            var pin = new NodeOutputExecPin(this, pinName);
            this.OutputExecPins.Insert(index ?? this.OutputExecPins.Count, pin);
            return pin;
        }

        /// <summary>
        /// Adds an input data pin to this node.
        /// </summary>
        /// <param name="pinName">Name of the pin.</param>
        /// <param name="index">Insertion index</param>
        protected NodeInputTypePin AddInputTypePin(string pinName, int? index = null)
        {
            var pin = new NodeInputTypePin(this, pinName);
            pin.IncomingPinChanged += OnIncomingTypePinChanged;
            this.InputTypePins.Insert(index ?? this.InputTypePins.Count, pin);
            return pin;
        }

        /// <summary>
        /// Adds an output data pin to this node.
        /// </summary>
        /// <param name="pinName">Name of the pin.</param>
        /// <param name="getOutputTypeFunc">Function that generates the output type.</param>
        /// <param name="index">Insertion index</param>
        protected NodeOutputTypePin AddOutputTypePin(string pinName, ObservableValue<BaseType> outputType, int? index = null)
        {
            var pin = new NodeOutputTypePin(this, pinName, outputType);
            this.OutputTypePins.Insert(index ?? this.InputDataPins.Count, pin);
            return pin;
        }

        private void OnIncomingTypePinChanged(NodeInputTypePin pin, NodeOutputTypePin oldPin, NodeOutputTypePin newPin)
        {
            if (oldPin?.InferredType != null)
                oldPin.InferredType.OnValueChanged -= EventInputTypeChanged;

            if (newPin?.InferredType != null)
                newPin.InferredType.OnValueChanged += EventInputTypeChanged;

            EventInputTypeChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called when anything about the input type arguments changes.
        /// </summary>
        public event EventHandler InputTypeChanged;

        private void EventInputTypeChanged(object sender, EventArgs eventArgs)
        {
            OnInputTypeChanged(sender, eventArgs);

            // Notify others afterwards, since the above call might have updated something
            InputTypeChanged?.Invoke(sender, eventArgs);
        }

        protected virtual void OnInputTypeChanged(object sender, EventArgs eventArgs)
        { }

        /// <summary>
        /// Called when the containing method was deserialized.
        /// </summary>
        public virtual void OnMethodDeserialized()
        {
            foreach (var inputTypePin in this.InputTypePins)
            {
                if (inputTypePin.InferredType != null)
                    inputTypePin.InferredType.OnValueChanged += EventInputTypeChanged;
                inputTypePin.IncomingPinChanged += OnIncomingTypePinChanged;
            }

            // Call OnInputTypeChanged to update the types of all nodes correctly.
            OnInputTypeChanged(this, null);
        }

        protected void RemovePin(NodePin pin)
        {
            GraphUtil.DisconnectPin(pin);

            if (pin is NodeInputDataPin idp)
            {
                this.InputDataPins.Remove(idp);
            }
            else if (pin is NodeOutputDataPin odp)
            {
                this.OutputDataPins.Replace(odp);
            }
            else if (pin is NodeInputExecPin ixp)
            {
                this.InputExecPins.Remove(ixp);
            }
            else if (pin is NodeOutputExecPin oxp)
            {
                this.OutputExecPins.Remove(oxp);
            }
            else if (pin is NodeInputTypePin itp)
            {
                this.InputTypePins.Remove(itp);
            }
            else if (pin is NodeOutputTypePin otp)
            {
                this.OutputTypePins.Remove(otp);
            }
            else
            {
                throw new NotImplementedException("Unknown pin type");
            }
        }
    }
}
