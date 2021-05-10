using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrintsEditor.Messages
{
    public class AddGerOrSetNodeMessage
    {
        public NodePin SuggestionPin { get; }
        public VariableSpecifier Variable { get; }

        public AddGerOrSetNodeMessage(VariableSpecifier variable, NodePin suggestionPin = null)
        {
            this.Variable = variable;
            this.SuggestionPin = suggestionPin;
        }
    }
}
