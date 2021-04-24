using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Represents a node that breaks execution of coroutine
    /// </summary>
    [DataContract]
    public class DelayNode : ExecNode
    {
        public DelayNode(MethodGraph graph) :
            base(graph)
        { }

        public override string ToString()
        {
            return "Delay";
        }
    }
}
