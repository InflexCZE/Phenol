using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Graph;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    [DataContract]
    [KnownType(typeof(MethodGraph))]
    [KnownType(typeof(ConstructorGraph))]
    [KnownType(typeof(ClassGraph))]
    [KnownType(typeof(TypeGraph))]
    public abstract class NodeGraph
    {
        /// <summary>
        /// Collection of nodes in this graph.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<Node> Nodes
        {
            get;
            private set;
        } = new ObservableRangeCollection<Node>();

        /// <summary>
        /// Class this graph is contained in.
        /// </summary>
        [DataMember]
        public ClassGraph Class
        {
            get;
            set;
        }

        /// <summary>
        /// Project the graph is part of.
        /// </summary>
        public Project Project
        {
            get;
            set;
        }

        /// <summary>
        /// Return node of this class that receives the metadata for it.
        /// </summary>
        public AttributesNode AttributesNode
        {
            get => this.Nodes.OfType<AttributesNode>().SingleOrDefault();
        }

        public IEnumerable<ConstructorNode> DefinedAttributes
        {
            get
            {
                if(this.AttributesNode?.InputDataPins is { } attributePins)
                {
                    foreach(var attributePin in attributePins)
                    {
                        if(attributePin.IncomingPin is { } pin)
                        {
                            yield return (ConstructorNode) pin.Node;
                        }
                    }
                }
            }
        }
    }
}
