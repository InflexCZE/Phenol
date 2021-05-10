using System.Linq;
using NetPrints.Core;
using System.Runtime.Serialization;

namespace NetPrints.Graph
{
    /// <summary>
    /// Abstract class for nodes that can be executed.
    /// </summary>
    [DataContract]
    [KnownType(typeof(CallMethodNode))]
    [KnownType(typeof(ConstructorNode))]
    public abstract class ExecNode : Node
    {
        protected ExecNode(NodeGraph graph)
            : base(graph)
        {
            if (graph is ExecutionGraph || this.CanSetPure == false)
            {
                AddExecPins();
            }
        }

        protected virtual void AddExecPins()
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("Exec");
        }

        protected virtual void RemoveExecPins()
        {
            foreach(var pin in this.InputExecPins.ToArray())
            {
                GraphUtil.DisconnectPin(pin);
                this.InputExecPins.Remove(pin);
            }

            foreach (var pin in this.OutputExecPins.ToArray())
            {
                GraphUtil.DisconnectPin(pin);
                this.OutputExecPins.Remove(pin);
            }
        }

        protected override void SetPurity(bool pure)
        {
            base.SetPurity(pure);

            if (pure)
            {
                RemoveExecPins();
            }
            else
            {
                AddExecPins();
            }
        }
    }
}
