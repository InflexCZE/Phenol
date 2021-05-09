using System.Runtime.Serialization;

namespace NetPrints.Graph
{
    public delegate void OutputExecPinOutgoingPinChangedDelegate(
        NodeOutputExecPin pin, NodeInputExecPin oldPin, NodeInputExecPin newPin);

    /// <summary>
    /// Pin which can be connected to an input execution pin to pass along execution.
    /// </summary>
    [DataContract]
    public class NodeOutputExecPin : NodeExecPin
    {
        /// <summary>
        /// Called when the connected outgoing pin changed.
        /// </summary>
        public event OutputExecPinOutgoingPinChangedDelegate OutgoingPinChanged;

        /// <summary>
        /// Connected input execution pin. Null if not connected.
        /// Can trigger OutgoingPinChanged when set.
        /// </summary>
        [DataMember]
        public NodeInputExecPin OutgoingPin
        {
            get => outgoingPin;
            set
            {
                if (outgoingPin != value)
                {
                    var oldPin = outgoingPin;

                    outgoingPin = value;

                    OutgoingPinChanged?.Invoke(this, oldPin, outgoingPin);
                }
            }
        }

        public override bool IsConnected => this.OutgoingPin is not null;
        
        private NodeInputExecPin outgoingPin;

        public NodeOutputExecPin(Node node, string name)
            : base(node, name)
        {
        }
    }
}
