using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Graph
{
    [DataContract]
    public class AttributesNode : Node
    {
        public AttributesNode(NodeGraph graph) :
            base(graph)
        {
            AddAttributeNode();
        }

        public void AddAttributeNode()
        {
            AddInputDataPin($"Attribute{this.InputTypePins.Count}", TypeSpecifier.FromType<Attribute>());
        }
        
        public void RemoveAttributeNode()
        {
            if (this.InputDataPins.LastOrDefault() is {} attributePin)
            {
                GraphUtil.DisconnectInputDataPin(attributePin);
                this.InputDataPins.Remove(attributePin);
            }
        }
    }
}
