using System;
using NetPrintsEditor.ViewModels;
using System.Collections.Generic;

namespace NetPrintsEditor.Messages
{
    /// <summary>
    /// Message for selecting and deselecting nodes.
    /// </summary>
    public class NodeSelectionMessage
    {
        public enum Mode
        {
            Add,
            Set,
            Toggle,
        }

        public Mode SelectionMode { get; }

        /// <summary>
        /// Nodes to be selected.
        /// </summary>
        public IEnumerable<NodeVM> Nodes { get; }

        /// <summary>
        /// Creates a node selection message to deselect all previous
        /// nodes and select the given node.
        /// </summary>
        /// <param name="nodes">Node to select.</param>
        public NodeSelectionMessage(IEnumerable<NodeVM> nodes, Mode mode)
        {
            this.Nodes = nodes;
            this.SelectionMode = mode;
        }

        /// <summary>
        /// Message to deselect all nodes.
        /// </summary>
        public static NodeSelectionMessage DeselectAll => new(Array.Empty<NodeVM>(), Mode.Set);
    }
}
