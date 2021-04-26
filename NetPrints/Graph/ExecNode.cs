﻿using NetPrints.Core;
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

        private void AddExecPins()
        {
            AddInputExecPin("Exec");
            AddOutputExecPin("Exec");
        }

        protected override void SetPurity(bool pure)
        {
            base.SetPurity(pure);

            if (pure)
            {
                GraphUtil.DisconnectInputExecPin(InputExecPins[0]);
                InputExecPins.RemoveAt(0);

                GraphUtil.DisconnectOutputExecPin(OutputExecPins[0]);
                OutputExecPins.RemoveAt(0);
            }
            else if (!pure)
            {
                AddExecPins();
            }
        }
    }
}
