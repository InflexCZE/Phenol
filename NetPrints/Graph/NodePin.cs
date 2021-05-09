using PropertyChanged;
using System.Runtime.Serialization;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract base class for node pins.
    /// </summary>
    [DataContract]
    [KnownType(typeof(NodeInputDataPin))]
    [KnownType(typeof(NodeOutputDataPin))]
    [KnownType(typeof(NodeInputExecPin))]
    [KnownType(typeof(NodeOutputExecPin))]
    [KnownType(typeof(NodeInputTypePin))]
    [KnownType(typeof(NodeOutputTypePin))]
    [AddINotifyPropertyChangedInterface]
    public abstract class NodePin
    {
        /// <summary>
        /// Name of the pin.
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// Node this pin is contained in.
        /// </summary>
        [DataMember]
        public Node Node { get; private set; }

        public abstract bool IsConnected { get; }

        protected NodePin(Node node, string name)
        {
            this.Node = node;
            this.Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
