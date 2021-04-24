using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Graph
{

    /// <summary>
    /// Node for iterating over data collection
    /// </summary>
    [DataContract]
    public class ForeachLoopNode : Node
    {
        /// <summary>
        /// Input execution pin that executes the loop.
        /// </summary>
        public NodeInputExecPin ExecutionPin
        {
            get { return InputExecPins[0]; }
        }

        /*
        /// <summary>
        /// When flow reaches this pin the loop stops immediately and continues to CompletedPin
        /// </summary>
        public NodeInputExecPin BreakPin
        {
            get { return InputExecPins[1]; }
        }
        */
        
        /// <summary>
        /// Execution pin that gets executed when the loop is over.
        /// </summary>
        public NodeOutputExecPin CompletedPin
        {
            get { return OutputExecPins[0]; }
        }
        
        /// <summary>
        /// Execution pin that gets executed for each loop.
        /// </summary>
        public NodeOutputExecPin LoopPin
        {
            get { return OutputExecPins[1]; }
        }

        /// <summary>
        /// Output data pin for the current index value of the loop.
        /// Starts at 0 and increments with every loop invocation by one
        /// </summary>
        public NodeOutputDataPin IndexPin
        {
            get { return OutputDataPins[0]; }
        }

        /// <summary>
        /// Output data pin for the current element value of the loop.
        /// </summary>
        public NodeOutputDataPin DataPin
        {
            get { return OutputDataPins[1]; }
        }

        /// <summary>
        /// Input data pin for the initial inclusive index value of the loop.
        /// </summary>
        public NodeInputDataPin DataCollectionPin
        {
            get { return InputDataPins[0]; }
        }

        public ForeachLoopNode(NodeGraph graph) :
            base(graph)
        {
            AddInputExecPin("Exec");
            //AddInputExecPin("Break");

            AddOutputExecPin("Completed");
            AddOutputExecPin("Loop");

            AddOutputDataPin("Index", TypeSpecifier.FromType<int>());
            AddOutputDataPin("Element", TypeSpecifier.FromType<object>());
            AddInputDataPin("Collection", TypeSpecifier.FromType<IEnumerable<object>>());
            
            SetupEvents();
            UpdateElementPin();
        }

        public override void OnMethodDeserialized()
        {
            base.OnMethodDeserialized();
            SetupEvents();
        }

        /// <summary>
        /// Sets up the task connection changed event which updates
        /// the result type.
        /// </summary>
        private void SetupEvents()
        {
            this.DataCollectionPin.IncomingPinChanged += (pin, oldPin, newPin) => UpdateElementPin();
        }

        /// <summary>
        /// Updates the result pin's type depending on the incoming task's return type.
        /// </summary>
        private void UpdateElementPin()
        {
            var collectionType = (TypeSpecifier) this.DataCollectionPin.IncomingPin?.PinType?.Value;
            
            
            //TODO: Do better
            var elementType = collectionType?.GenericArguments[0];

            if(elementType == null)
                return;

            if(elementType == this.DataPin.PinType.Value)
                return;

            this.DataPin.PinType.Value = elementType;

            // Disconnect all existing connections.
            // Might want them to stay connected but that requires reflection
            // to determine if the types are still compatible.
            GraphUtil.DisconnectOutputDataPin(this.DataPin);
        }

        public override string ToString()
        {
            return "Foreach loop";
        }
    }
}