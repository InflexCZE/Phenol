using NetPrints.Core;
using NetPrints.Graph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Utils;

namespace NetPrints.Translator
{
    /// <summary>
    /// Translates execution graphs into C#.
    /// </summary>
    public class ExecutionGraphTranslator
    {
        private const string JumpStackVarName = "jumpStack";
        private const string JumpStackType = "System.Collections.Generic.Stack<int>";

        private readonly Dictionary<NodeOutputDataPin, string> variableNames = new Dictionary<NodeOutputDataPin, string>();
        private readonly Dictionary<Node, List<int>> nodeStateIds = new Dictionary<Node, List<int>>();
        private int nextStateId = 0;
        private IEnumerable<Node> execNodes = new List<Node>();
        private IEnumerable<Node> nodes = new List<Node>();
        private readonly HashSet<NodeInputExecPin> pinsJumpedTo = new HashSet<NodeInputExecPin>();

        private int jumpStackStateId;

        private readonly StringBuilder builder = new StringBuilder();

        private ExecutionGraph graph;

        //TODO: Produce stable code
        private Random random;

        private delegate void NodeTypeHandler(ExecutionGraphTranslator translator, Node node);

        private readonly Dictionary<Type, List<NodeTypeHandler>> nodeTypeHandlers = new Dictionary<Type, List<NodeTypeHandler>>()
        {
            { typeof(CallMethodNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateCallMethodNode(node as CallMethodNode) } },
            { typeof(VariableSetterNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateVariableSetterNode(node as VariableSetterNode) } },
            { typeof(DelayNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateDelayNode(node as DelayNode) } },
            { typeof(ReturnNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateReturnNode(node as ReturnNode) } },
            { typeof(MethodEntryNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateMethodEntry(node as MethodEntryNode) } },
            { typeof(IfElseNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateIfElseNode(node as IfElseNode) } },
            { typeof(ConstructorNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateConstructorNode(node as ConstructorNode) } },
            { typeof(ExplicitCastNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateExplicitCastNode(node as ExplicitCastNode) } },
            { typeof(ThrowNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateThrowNode(node as ThrowNode) } },
            { typeof(AwaitNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateAwaitNode(node as AwaitNode) } },
            { typeof(TernaryNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateTernaryNode(node as TernaryNode) } },

            { typeof(ForLoopNode), new List<NodeTypeHandler>
              {
                (translator, node) => translator.TranslateStartForLoopNode(node as ForLoopNode),
                (translator, node) => translator.TranslateContinueForLoopNode(node as ForLoopNode)
              }
            },

            { typeof(ForeachLoopNode), new List<NodeTypeHandler> 
              {
                (translator, node) => translator.TranslateForeachLoopNode(node as ForeachLoopNode),
                (translator, node) => translator.TranslateBreakForeachLoopNode(node as ForeachLoopNode),
              }
            },

            { typeof(RerouteNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateRerouteNode(node as RerouteNode) } },

            { typeof(VariableGetterNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateVariableGetterNode(node as VariableGetterNode) } },
            { typeof(LiteralNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateLiteralNode(node as LiteralNode) } },
            { typeof(MakeDelegateNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateMakeDelegateNode(node as MakeDelegateNode) } },
            { typeof(TypeOfNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateTypeOfNode(node as TypeOfNode) } },
            { typeof(MakeArrayNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateMakeArrayNode(node as MakeArrayNode) } },
            { typeof(DefaultNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateDefaultNode(node as DefaultNode) } },
        };

        private int GetNextStateId()
        {
            return nextStateId++;
        }

        private int GetExecPinStateId(NodeInputExecPin pin)
        {
            return nodeStateIds[pin.Node][pin.Node.InputExecPins.IndexOf(pin)];
        }

        private string GetOrCreatePinName(NodeOutputDataPin pin)
        {
            // Return the default value of the pin type if nothing is connected
            if (pin == null)
            {
                return "null";
            }

            if (variableNames.ContainsKey(pin))
            {
                return variableNames[pin];
            }

            string pinName;

            // Special case for property setters, input name "value".
            // TODO: Don't rely on set_ prefix
            // TODO: Use PropertyGraph instead of MethodGraph
            if (pin.Node is MethodEntryNode && graph is MethodGraph methodGraph && methodGraph.Name.StartsWith("set_"))
            {
                pinName = "value";
            }
            else
            {
                pinName = TranslatorUtil.GetUniqueVariableName(pin.Name.Replace("<", "_").Replace(">", "_"), variableNames.Values.ToList());
            }

            variableNames.Add(pin, pinName);
            return pinName;
        }

        private string GetPinIncomingValue(NodeInputDataPin pin)
        {
            if (pin.IncomingPin == null)
            {
                var valueType = (TypeSpecifier)pin.PinType.Value;
                
                if (pin.UsesUnconnectedValue && pin.UnconnectedValue != null)
                {
                    return TranslatorUtil.ObjectToLiteral(pin.UnconnectedValue, valueType);
                }
                
                if (pin.UsesExplicitDefaultValue)
                {
                    return TranslatorUtil.ObjectToLiteral(pin.ExplicitDefaultValue, valueType);
                }

                throw new Exception($"Input data pin {pin} on {pin.Node} was unconnected without an explicit default or unconnected value.");
                //return $"default({pin.PinType.Value.FullCodeName})";
            }

            return GetOrCreatePinName(pin.IncomingPin);
        }

        private string[] GetOrCreatePinNames(IEnumerable<NodeOutputDataPin> pins)
        {
            return pins.Select(pin => GetOrCreatePinName(pin)).ToArray();
        }

        private string[] GetPinIncomingValues(IEnumerable<NodeInputDataPin> pins)
        {
            return pins.Select(pin => GetPinIncomingValue(pin)).ToArray();
        }

        private string GetOrCreateTypedPinName(NodeOutputDataPin pin)
        {
            string pinName = GetOrCreatePinName(pin);
            return $"{pin.PinType.Value.FullCodeName} {pinName}";
        }

        private IEnumerable<string> GetOrCreateTypedPinNames(IEnumerable<NodeOutputDataPin> pins)
        {
            return pins.Select(pin => GetOrCreateTypedPinName(pin));
        }

        private void CreateStates()
        {
            foreach(Node node in execNodes)
            {
                if (!(node is MethodEntryNode))
                {
                    nodeStateIds.Add(node, new List<int>());

                    foreach (NodeInputExecPin execPin in node.InputExecPins)
                    {
                        nodeStateIds[node].Add(GetNextStateId());
                    }
                }
            }
        }

        private void CreateVariables()
        {
            foreach(Node node in nodes)
            {
                var v = GetOrCreatePinNames(node.OutputDataPins);
            }
        }

        private void TranslateVariables()
        {
            builder.AppendLine("// Variables");

            foreach (var v in variableNames)
            {
                NodeOutputDataPin pin = v.Key;
                string variableName = v.Value;

                if (!(pin.Node is MethodEntryNode))
                {
                    builder.AppendLine($"{pin.PinType.Value.FullCodeName} {variableName} = default({pin.PinType.Value.FullCodeName});");
                }
            }
        }

        private void TranslateSignature()
        {
            builder.AppendLine($"// {graph}");

            // Write visibility
            builder.Append($"{TranslatorUtil.VisibilityTokens[graph.Visibility]} ");

            var methodGraph = graph as MethodGraph;

            if (methodGraph != null)
            {
                // Write modifiers
                var modifiers = methodGraph.Modifiers.Value;
                if (modifiers.HasFlag(MethodModifiers.Async))
                {
                    builder.Append("async ");
                }

                if (modifiers.HasFlag(MethodModifiers.Static))
                {
                    builder.Append("static ");
                }

                if (modifiers.HasFlag(MethodModifiers.Abstract))
                {
                    builder.Append("abstract ");
                }

                if (modifiers.HasFlag(MethodModifiers.Sealed))
                {
                    builder.Append("sealed ");
                }

                if (modifiers.HasFlag(MethodModifiers.Override))
                {
                    builder.Append("override ");
                }
                else if (modifiers.HasFlag(MethodModifiers.Virtual))
                {
                    builder.Append("virtual ");
                }

                // Write return type
                var isCoroutine = methodGraph.IsCoroutine;
                if(isCoroutine)
                {
                    builder.Append(TypeSpecifier.FromType<IEnumerator>().FullCodeName);
                }
                else
                {
                    var returnTypes = methodGraph.ReturnTypes;
                    if(returnTypes.Count == 0)
                    {
                        builder.Append("void");
                    }
                    else
                    {
                        var returnType = returnTypes.Count == 1 ? returnTypes[0] : GenericsHelper.BuildAggregateType(returnTypes);
                        builder.AppendLine(returnType.FullCodeName);
                    }
                }

                builder.Append(' ');
            }

            // Write name
            builder.Append(graph.ToString());

            if (methodGraph != null)
            {
                // Write generic arguments if any
                if (methodGraph.GenericArgumentTypes.Any())
                {
                    builder.Append("<" + string.Join(", ", methodGraph.GenericArgumentTypes.Select(arg => arg.FullCodeName)) + ">");
                }
            }

            // Write parameters
            var args = GetOrCreateTypedPinNames(graph.EntryNode.OutputDataPins);
            
            if(methodGraph?.IsCoroutine == true)
            {
                if(methodGraph.ReturnTypes is { Count: > 0 } returnTypes)
                {
                    var returnType = CoroutineUtils.GetCoroutineReturnType(returnTypes);
                    args = args.Append($"{returnType.FullCodeName} {CoroutineUtils.CoroutineReturnArgName}");
                }
            }

            builder.AppendLine($"({string.Join(", ", args)})");
        }

        private void TranslateJumpStack()
        {
            builder.AppendLine("// Jump stack");

            builder.AppendLine($"State{jumpStackStateId}:");
            builder.AppendLine($"if ({JumpStackVarName}.Count == 0) throw new System.Exception();");
            builder.AppendLine($"switch ({JumpStackVarName}.Pop())");
            builder.AppendLine("{");

            foreach (NodeInputExecPin pin in pinsJumpedTo)
            {
                builder.AppendLine($"case {GetExecPinStateId(pin)}:");
                WriteGotoInputPin(pin);
            }

            builder.AppendLine("default:");
            builder.AppendLine("throw new System.Exception();");

            builder.AppendLine("}"); // End switch
        }

        /// <summary>
        /// Translates a method to C#.
        /// </summary>
        /// <param name="graph">Execution graph to translate.</param>
        /// <param name="withSignature">Whether to translate the signature.</param>
        /// <returns>C# code for the method.</returns>
        public string Translate(ExecutionGraph graph, bool withSignature)
        {
            this.graph = graph;

            // Reset state
            variableNames.Clear();
            nodeStateIds.Clear();
            pinsJumpedTo.Clear();
            nextStateId = 0;
            builder.Clear();
            random = new Random(0);

            nodes = TranslatorUtil.GetAllNodesInExecGraph(graph);
            execNodes = TranslatorUtil.GetExecNodesInExecGraph(graph);

            // Assign a state id to every non-pure node
            CreateStates();

            // Assign jump stack state id
            // Write it later once we know which states get jumped to
            jumpStackStateId = GetNextStateId();

            // Create variables for all output pins for every node
            CreateVariables();

            // Write the signatures
            if (withSignature)
            {
                TranslateSignature();
            }

            builder.AppendLine("{"); // Method start

            // Write a placeholder for the jump stack declaration
            // Replaced later
            builder.Append("%JUMPSTACKPLACEHOLDER%");

            // Write the variable declarations
            TranslateVariables();
            builder.AppendLine();

            // Start at node after method entry if necessary (id!=0)
            if (graph.EntryNode.OutputExecPins[0].OutgoingPin != null && GetExecPinStateId(graph.EntryNode.OutputExecPins[0].OutgoingPin) != 0)
            {
                WriteGotoOutputPin(graph.EntryNode.OutputExecPins[0]);
            }

            // Translate every exec node
            TranslateNodeChain(execNodes);

            // Write the jump stack if it was ever used
            if (pinsJumpedTo.Count > 0)
            {
                TranslateJumpStack();

                builder.Replace("%JUMPSTACKPLACEHOLDER%", $"{JumpStackType} {JumpStackVarName} = new {JumpStackType}();{Environment.NewLine}");
            }
            else
            {
                builder.Replace("%JUMPSTACKPLACEHOLDER%", "");
            }

            builder.AppendLine("}"); // Method end

            string code = builder.ToString();

            // Remove unused labels
            return RemoveUnnecessaryLabels(code);
        }

        private void TranslateNodeChain(IEnumerable<Node> nodesToTranslate)
        {
            var method = this.graph;
            var @class = graph.Class;
            var project = @class.Project;
            var classIndex = project.Classes.IndexOf(@class);
            var methodIndex = @class.Methods.Cast<NodeGraph>().Concat(@class.Constructors).IndexOf(method);

            string lifeLinkFormat = null;

            if(classIndex >= 0 && methodIndex >= 0)
            {
                //TODO: Load from dynamic source
                switch(project.LiveLinkType)
                {
                    case "SpaceLink":
                        lifeLinkFormat = "__HIT__.Hit({0}, {1}, {2}, {3});";
                        break;
                    
                    case Project.LiveLinkTypeNone:
                        lifeLinkFormat = null;
                        break;
                    
                    default:
                        Debug.Fail("Unknown LifeLink type");
                        goto case Project.LiveLinkTypeNone;
                }
            }

            foreach (var node in nodesToTranslate)
            {
                if (!(node is MethodEntryNode))
                {
                    var nodeIndex = method.Nodes.IndexOf(node);

                    for (int pinIndex = 0; pinIndex < node.InputExecPins.Count; pinIndex++)
                    {
                        builder.AppendLine($"State{nodeStateIds[node][pinIndex]}:");

                        if(nodeIndex >= 0 && lifeLinkFormat is not null)
                        {
                            builder.AppendLine(string.Format(lifeLinkFormat, classIndex, methodIndex, nodeIndex, pinIndex));
                        }
                        
                        TranslateNode(node, pinIndex);
                        builder.AppendLine();
                    }
                }
            }
        }

        private string RemoveUnnecessaryLabels(string code)
        {
            foreach (int stateId in nodeStateIds.Values.SelectMany(i => i))
            {
                if (!code.Contains($"goto State{stateId};"))
                {
                    code = code.Replace($"State{stateId}:", "");
                }
            }

            return code;
        }

        public void TranslateNode(Node node, int pinIndex)
        {
            if (!(node is RerouteNode))
            {
                builder.AppendLine($"// {node}");
            }

            if (nodeTypeHandlers.ContainsKey(node.GetType()))
            {
                nodeTypeHandlers[node.GetType()][pinIndex](this, node);
            }
            else
            {
                Debug.WriteLine($"Unhandled type {node.GetType()} in TranslateNode");
            }
        }

        private void WriteGotoJumpStack()
        {
            builder.AppendLine($"goto State{jumpStackStateId};");
        }

        private void WritePushJumpStack(NodeInputExecPin pin)
        {
            if (!pinsJumpedTo.Contains(pin))
            {
                pinsJumpedTo.Add(pin);
            }

            builder.AppendLine($"{JumpStackVarName}.Push({GetExecPinStateId(pin)});");
        }

        private void WriteGotoInputPin(NodeInputExecPin pin)
        {
            builder.AppendLine($"goto State{GetExecPinStateId(pin)};");
        }

        private void WriteGotoOutputPin(NodeOutputExecPin pin)
        {
            if (pin.OutgoingPin == null)
            {
                WriteGotoJumpStack();
            }
            else
            {
                WriteGotoInputPin(pin.OutgoingPin);
            }
        }

        private void WriteGotoOutputPinIfNecessary(NodeOutputExecPin pin, NodeInputExecPin fromPin)
        {
            int fromId = GetExecPinStateId(fromPin);
            int nextId = fromId + 1;

            if (pin.OutgoingPin == null)
            {
                if (nextId != jumpStackStateId)
                {
                    WriteGotoJumpStack();
                }
            }
            else
            {
                int toId = GetExecPinStateId(pin.OutgoingPin);

                // Only write the goto if the next state is not
                // the state we want to go to.
                if (nextId != toId)
                {
                    WriteGotoInputPin(pin.OutgoingPin);
                }
            }
        }

        public void TranslateDependentPureNodes(Node node)
        {
            var sortedPureNodes = TranslatorUtil.GetSortedPureNodes(node);
            foreach(Node depNode in sortedPureNodes)
            {
                TranslateNode(depNode, 0);
            }
        }

        public void TranslateMethodEntry(MethodEntryNode node)
        {
            /*// Go to the next state.
            // Only write if it's not the initial state (id==0) anyway.
            if (node.OutputExecPins[0].OutgoingPin != null && GetExecPinStateId(node.OutputExecPins[0].OutgoingPin) != 0)
            {
                WriteGotoOutputPin(node.OutputExecPins[0]);
            }*/
        }

        public void TranslateCallMethodNode(CallMethodNode node)
        {
            // Wrap in try / catch
            if (node.HandlesExceptions)
            {
                builder.AppendLine("try");
                builder.AppendLine("{");
            }

            string temporaryReturnName = null;

            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Write assignment of return values
            var returnValuePins = node.ReturnValuePins;
            var returnTypes = returnValuePins.Select(x => x.PinType.Value);
            
            if (node.IsCoroutine)
            {
                if(node.NaturalSignature && returnValuePins.Count > 0)
                {
                    temporaryReturnName = TranslatorUtil.GetTemporaryVariableName(random);
                    builder.Append($"var {temporaryReturnName} = new {CoroutineUtils.GetCoroutineReturnType(returnTypes)}();");
                }
                else
                {
                    
                }

                if(node.IsInCoroutine)
                {
                    builder.Append("yield return ");
                }
            }

            if(node.IsCoroutine == false || node.IsInCoroutine == false)
            {
                if (returnValuePins.Count == 1)
                {
                    string returnName = GetOrCreatePinName(returnValuePins[0]);
                    builder.Append($"{returnName} = ");
                }
                else if (returnValuePins.Count > 1)
                {
                    Debug.Assert(node.IsCoroutine == false);
                    Debug.Assert(temporaryReturnName is null);
                    temporaryReturnName = TranslatorUtil.GetTemporaryVariableName(random);

                    var tempType = GenericsHelper.BuildAggregateType(returnTypes);
                    builder.Append($"{tempType.FullCodeName} {temporaryReturnName} = ");
                }
            }

            // Get arguments for method call
            var argumentValues = GetPinIncomingValues(node.ArgumentPins);

            // Check whether the method is an operator and we need to translate its name
            // into operator symbols. Otherwise just call the method normally.
            if (OperatorUtil.TryGetOperatorInfo(node.MethodSpecifier, out OperatorInfo operatorInfo))
            {
                Debug.Assert(node.IsCoroutine == false);
                Debug.Assert(!argumentValues.Any(a => a is null));

                if (operatorInfo.Unary)
                {
                    if (argumentValues.Length != 1)
                    {
                        throw new Exception($"Unary operator was found but did not have one argument: {node.MethodName}");
                    }

                    if (operatorInfo.UnaryRightPosition)
                    {
                        builder.AppendLine($"{argumentValues[0]}{operatorInfo.Symbol};");
                    }
                    else
                    {
                        builder.AppendLine($"{operatorInfo.Symbol}{argumentValues[0]};");
                    }
                }
                else
                {
                    if (argumentValues.Length != 2)
                    {
                        throw new Exception($"Binary operator was found but did not have two arguments: {node.MethodName}");
                    }

                    builder.AppendLine($"{argumentValues[0]}{operatorInfo.Symbol}{argumentValues[1]};");
                }
            }
            else
            {
                // Static: Write class name / target, default to own class name
                // Instance: Write target, default to `this`

                if (node.IsStatic)
                {
                    builder.Append($"{node.DeclaringType.FullCodeName}.");
                }
                else
                {
                    if (node.TargetPin.IncomingPin is {} incomingPin)
                    {
                        string targetName = GetOrCreatePinName(incomingPin);
                        builder.Append($"{targetName}.");
                    }
                    else
                    {
                        // Default to this
                        builder.Append("this.");
                    }
                }

                var parameterDefinitions = node.MethodSpecifier.Parameters;
                
                if(node.NaturalSignature == false && node.MethodSpecifier.ReturnTypes.Count > 0)
                {
                    parameterDefinitions = parameterDefinitions.Append(new MethodParameter
                    (
                        CoroutineUtils.CoroutineReturnArgName,
                        null,
                        MethodParameterPassType.Default,
                        false,
                        null
                    )).ToArray();
                }
                
                Debug.Assert(argumentValues.Length == parameterDefinitions.Count);

                bool prependArgumentName = false;
                var arguments = new List<string>();
                foreach (var (argValue, methodParameter) in argumentValues.Zip(parameterDefinitions, ValueTuple.Create))
                {
                    if(argValue is null)
                    {
                        //null means use default value
                        //from now on we need to name parameters explicitly
                        prependArgumentName = true;
                    }
                    else
                    {
                        string argument = argValue;

                        // Prepend with argument name if wanted
                        if (prependArgumentName)
                        {
                            argument = $"{methodParameter.Name}: {argument}";
                        }

                        // Prefix with "out" / "ref" / "in"
                        switch (methodParameter.PassType)
                        {
                            case MethodParameterPassType.Out:
                                argument = "out " + argument;
                                break;
                            
                            case MethodParameterPassType.Reference:
                                argument = "ref " + argument;
                                break;
                            
                            case MethodParameterPassType.In:
                                // Don't pass with in as it could break implicit casts.
                                // argument = "in " + argument;
                                break;
                        }

                        arguments.Add(argument);
                    }
                }

                if(temporaryReturnName is not null && node.IsCoroutine)
                {
                    var coroutineReturnStorage = temporaryReturnName;
                    temporaryReturnName += '.' + CoroutineUtils.CoroutineReturnValueField;
                    
                    if(prependArgumentName)
                    {
                        coroutineReturnStorage = $"{CoroutineUtils.CoroutineReturnArgName}: {coroutineReturnStorage}";
                    }

                    arguments.Add(coroutineReturnStorage);
                }

                // Write the method call
                builder.AppendLine($"{node.BoundMethodName}({string.Join(", ", arguments)});");
            }

            // Assign the real variables from the temporary tuple
            if (temporaryReturnName is not null)
            {
                var returnNames = GetOrCreatePinNames(returnValuePins);
                for(int i = 0; i < returnNames.Length; i++)
                {
                    builder.AppendLine($"{returnNames[i]} = {temporaryReturnName}.Item{i+1};");
                }
            }

            // Set the exception to null on success if catch pin is connected
            if (node.HandlesExceptions)
            {
                builder.AppendLine($"{GetOrCreatePinName(node.ExceptionPin)} = null;");
            }

            // Go to the next state
            if (!node.IsPure)
            {
                WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
            }

            // Catch exceptions if catch pin is connected
            if (node.HandlesExceptions)
            {
                string exceptionVarName = TranslatorUtil.GetTemporaryVariableName(random);
                builder.AppendLine("}");
                builder.AppendLine($"catch (System.Exception {exceptionVarName})");
                builder.AppendLine("{");
                builder.AppendLine($"{GetOrCreatePinName(node.ExceptionPin)} = {exceptionVarName};");

                // Set all return values to default on exception
                foreach (var returnValuePin in returnValuePins)
                {
                    string returnName = GetOrCreatePinName(returnValuePin);
                    builder.AppendLine($"{returnName} = default({returnValuePin.PinType.Value.FullCodeName});");
                }

                if (!node.IsPure)
                {
                    WriteGotoOutputPinIfNecessary(node.CatchPin, node.InputExecPins[0]);
                }

                builder.AppendLine("}");
            }
        }

        public void TranslateConstructorNode(ConstructorNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Write assignment and constructor
            string returnName = GetOrCreatePinName(node.OutputDataPins[0]);
            builder.Append($"{returnName} = new {node.ClassType}");

            // Write constructor arguments
            var argumentNames = GetPinIncomingValues(node.ArgumentPins);
            //builder.AppendLine($"({string.Join(", ", argumentNames)});");

            Debug.Assert(argumentNames.Length == node.ConstructorSpecifier.Arguments.Count);

            bool prependArgumentName = argumentNames.Any(a => a is null);

            List<string> arguments = new List<string>();

            foreach ((var argName, var constructorParameter) in argumentNames.Zip(node.ConstructorSpecifier.Arguments, Tuple.Create))
            {
                // null means use default value
                if (!(argName is null))
                {
                    string argument = argName;

                    // Prepend with argument name if wanted
                    if (prependArgumentName)
                    {
                        argument = $"{constructorParameter.Name}: {argument}";
                    }

                    // Prefix with "out" / "ref" / "in"
                    switch (constructorParameter.PassType)
                    {
                        case MethodParameterPassType.Out:
                            argument = "out " + argument;
                            break;
                        case MethodParameterPassType.Reference:
                            argument = "ref " + argument;
                            break;
                        case MethodParameterPassType.In:
                            // Don't pass with in as it could break implicit casts.
                            // argument = "in " + argument;
                            break;
                        default:
                            break;
                    }

                    arguments.Add(argument);
                }
            }

            // Write the method call
            builder.AppendLine($"({string.Join(", ", arguments)});");

            if (!node.IsPure)
            {
                // Go to the next state
                WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
            }
        }

        public void TranslateExplicitCastNode(ExplicitCastNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Try to cast the incoming object and go to next states.
            if (node.ObjectToCast.IncomingPin != null)
            {
                string pinToCastName = GetPinIncomingValue(node.ObjectToCast);
                string outputName = GetOrCreatePinName(node.CastPin);

                // If failure pin is not connected write explicit cast that throws.
                // Otherwise check if cast object is null and execute failure
                // path if it is.
                if (node.IsPure || node.CastFailedPin.OutgoingPin == null)
                {
                    builder.AppendLine($"{outputName} = ({node.CastType.FullCodeNameUnbound}){pinToCastName};");

                    if (!node.IsPure)
                    {
                        WriteGotoOutputPinIfNecessary(node.CastSuccessPin, node.InputExecPins[0]);
                    }
                }
                else
                {
                    builder.AppendLine($"{outputName} = {pinToCastName} as {node.CastType.FullCodeNameUnbound};");

                    if (!node.IsPure)
                    {
                        builder.AppendLine($"if ({outputName} is null)");
                        builder.AppendLine("{");
                        WriteGotoOutputPinIfNecessary(node.CastFailedPin, node.InputExecPins[0]);
                        builder.AppendLine("}");
                        builder.AppendLine("else");
                        builder.AppendLine("{");
                        WriteGotoOutputPinIfNecessary(node.CastSuccessPin, node.InputExecPins[0]);
                        builder.AppendLine("}");
                    }
                }
            }
        }

        public void TranslateThrowNode(ThrowNode node)
        {
            TranslateDependentPureNodes(node);
            builder.AppendLine($"throw {GetPinIncomingValue(node.ExceptionPin)};");
        }

        public void TranslateAwaitNode(AwaitNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Store result if task has a return value.
            if (node.ResultPin != null)
            {
                builder.Append($"{GetOrCreatePinName(node.ResultPin)} = ");
            }

            builder.AppendLine($"await {GetPinIncomingValue(node.TaskPin)};");
        }

        public void TranslateTernaryNode(TernaryNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            builder.Append($"{GetOrCreatePinName(node.OutputObjectPin)} = ");
            builder.Append($"{GetPinIncomingValue(node.ConditionPin)} ? ");
            builder.Append($"{GetPinIncomingValue(node.TrueObjectPin)} : ");
            builder.AppendLine($"{GetPinIncomingValue(node.FalseObjectPin)};");

            if (!node.IsPure)
            {
                WriteGotoOutputPinIfNecessary(node.OutputExecPins.Single(), node.InputExecPins.Single());
            }
        }

        public void TranslateVariableSetterNode(VariableSetterNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            string valueName = GetPinIncomingValue(node.NewValuePin);

            // Add target name if there is a target (null for local and static variables)
            if (node.IsStatic)
            {
                if (!(node.TargetType is null))
                {
                    builder.Append(node.TargetType.FullCodeName);
                }
                else
                {
                    builder.Append(node.Graph.Class.Name);
                }
            }
            if (node.TargetPin != null)
            {
                if (node.TargetPin.IncomingPin != null)
                {
                    string targetName = GetOrCreatePinName(node.TargetPin.IncomingPin);
                    builder.Append(targetName);
                }
                else
                {
                    builder.Append("this");
                }
            }

            // Add index if needed
            if (node.IsIndexer)
            {
                builder.Append($"[{GetPinIncomingValue(node.IndexPin)}]");
            }
            else
            {
                builder.Append($".{node.VariableName}");
            }

            builder.AppendLine($" = {valueName};");

            // Set output pin of this node to the same value
            builder.AppendLine($"{GetOrCreatePinName(node.OutputDataPins[0])} = {valueName};");

            // Go to the next state
            WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
        }

        public void TranslateDelayNode(DelayNode node)
        {
            TranslateDependentPureNodes(node);
            builder.AppendLine("yield return null;");
        }

        public void TranslateReturnNode(ReturnNode node)
        {
            TranslateDependentPureNodes(node);

            string value = null;
            var returnPins = node.InputDataPins;
            var returnValues = returnPins.Select(pin => GetPinIncomingValue(pin));

            if(returnPins.Count == 1 && node.IsInCoroutine == false)
            {
                value = returnValues.Single();
            }
            else if(returnPins.Count >= 1)
            {
                var returnType = GenericsHelper.BuildAggregateType(returnPins.Select(pin => pin.PinType.Value));
                value = $"new {returnType.FullCodeName}({string.Join(", ", returnValues)})";
                
                if(node.IsInCoroutine)
                {
                    builder.AppendLine($"{CoroutineUtils.CoroutineReturnArgName}.{CoroutineUtils.CoroutineReturnValueField} = {value};");
                }
            }

            if (returnPins.Count == 0 || node.IsInCoroutine)
            {
                EmitControlFlowEnd(node.InputExecPins[0]);
            }
            else
            {
                if(returnPins.Count == 1 && returnPins[0].PinType == TypeSpecifier.FromType<Task>())
                {
                    // Special case for async functions returning Task (no return value)
                    builder.AppendLine("return;");
                }
                else
                {
                    builder.AppendLine($"return {value};");
                }
            }
        }

        public void TranslateIfElseNode(IfElseNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            string conditionVar = GetPinIncomingValue(node.ConditionPin);

            builder.AppendLine($"if ({conditionVar})");
            builder.AppendLine("{");

            if (node.TruePin.OutgoingPin != null)
            {
                WriteGotoOutputPinIfNecessary(node.TruePin, node.InputExecPins[0]);
            }
            else
            {
                EmitControlFlowEnd(node.ExecutionPin);
            }

            builder.AppendLine("}");

            builder.AppendLine("else");
            builder.AppendLine("{");

            if (node.FalsePin.OutgoingPin != null)
            {
                WriteGotoOutputPinIfNecessary(node.FalsePin, node.InputExecPins[0]);
            }
            else
            {
                EmitControlFlowEnd(node.ExecutionPin);
            }

            builder.AppendLine("}");
        }

        public void TranslateStartForLoopNode(ForLoopNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            //builder.AppendLine($"{GetOrCreatePinName(node.IndexPin)} = {GetPinIncomingValue(node.InitialIndexPin)};");
            //builder.AppendLine($"if ({GetOrCreatePinName(node.IndexPin)} < {GetPinIncomingValue(node.MaxIndexPin)})");
            //builder.AppendLine("{");
            //WritePushJumpStack(node.ContinuePin);
            //WriteGotoOutputPinIfNecessary(node.LoopPin, node.ExecutionPin);
            //builder.AppendLine("}");

            var index = GetOrCreatePinName(node.IndexPin);
            builder.AppendLine($"for({index} = {GetPinIncomingValue(node.InitialIndexPin)}; {index} < {GetPinIncomingValue(node.MaxIndexPin)}; {index}++)");
            builder.AppendLine("{");
            TranslateLoopBody(node.LoopPin);
            builder.AppendLine("}");
        }

        public void TranslateContinueForLoopNode(ForLoopNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            builder.AppendLine($"{GetOrCreatePinName(node.IndexPin)}++;");
            builder.AppendLine($"if ({GetOrCreatePinName(node.IndexPin)} < {GetPinIncomingValue(node.MaxIndexPin)})");
            builder.AppendLine("{");
            WritePushJumpStack(node.ContinuePin);
            WriteGotoOutputPinIfNecessary(node.LoopPin, node.ContinuePin);
            builder.AppendLine("}");

            WriteGotoOutputPinIfNecessary(node.CompletedPin, node.ContinuePin);
        }

        public void TranslateForeachLoopNode(ForeachLoopNode node)
        {
            TranslateDependentPureNodes(node);
            
            var elementName = GetOrCreatePinName(node.DataPin);
            var indexName = GetOrCreatePinName(node.IndexPin);
            var tmpElementName = $"__{elementName}_tmp__";

            builder.AppendLine($"{indexName} = -1;");
            builder.AppendLine($"foreach(var {tmpElementName} in {GetOrCreatePinName(node.DataCollectionPin.IncomingPin)})");
            builder.AppendLine("{");

            builder.AppendLine($"{indexName}++;");
            builder.AppendLine($"{elementName} = {tmpElementName};");
            TranslateLoopBody(node.LoopPin);

            builder.AppendLine("}");
        }

        public void TranslateBreakForeachLoopNode(ForeachLoopNode node)
        {
            //WriteGotoOutputPinIfNecessary(node.CompletedPin, node.BreakPin);
        }

        public void TranslateLoopBody(NodeOutputExecPin loopPin)
        {
            var node = loopPin.Node;
            var loopBeginNode = loopPin?.OutgoingPin?.Node;
            if (loopBeginNode is not null)
            {
                //TODO: This is hack af
                //WriteGotoOutputPinIfNecessary(node.LoopPin, node.ExecutionPin);
                var nodes = new HashSet<Node>();
                TranslatorUtil.AddExecNodes(loopBeginNode, nodes);
                nodes.Remove(node);
                TranslateNodeChain(nodes);
            }
        }

        public void PureTranslateVariableGetterNode(VariableGetterNode node)
        {
            string valueName = GetOrCreatePinName(node.OutputDataPins[0]);

            builder.Append($"{valueName} = ");

            if (node.IsStatic)
            {
                if (!(node.TargetType is null))
                {
                    builder.Append(node.TargetType.FullCodeName);
                }
                else
                {
                    builder.Append(node.Graph.Class.Name);
                }
            }
            else
            {
                if (node.TargetPin?.IncomingPin != null)
                {
                    string targetName = GetOrCreatePinName(node.TargetPin.IncomingPin);
                    builder.Append(targetName);
                }
                else
                {
                    // Default to this
                    builder.Append("this");
                }
            }

            // Add index if needed
            if (node.IsIndexer)
            {
                builder.Append($"[{GetPinIncomingValue(node.IndexPin)}]");
            }
            else
            {
                builder.Append($".{node.VariableName}");
            }

            builder.AppendLine(";");
        }

        public void PureTranslateLiteralNode(LiteralNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.ValuePin)} = {GetPinIncomingValue(node.InputDataPins[0])};");
        }

        public void PureTranslateMakeDelegateNode(MakeDelegateNode node)
        {
            // Write assignment of return value
            string returnName = GetOrCreatePinName(node.OutputDataPins[0]);
            builder.Append($"{returnName} = ");

            // Static: Write class name / target, default to own class name
            // Instance: Write target, default to this

            if (node.IsFromStaticMethod)
            {
                builder.Append($"{node.MethodSpecifier.DeclaringType}.");
            }
            else
            {
                if (node.TargetPin.IncomingPin != null)
                {
                    string targetName = GetOrCreatePinName(node.TargetPin.IncomingPin);
                    builder.Append($"{targetName}.");
                }
                else
                {
                    // Default to thise
                    builder.Append("this.");
                }
            }

            // Write method name
            builder.AppendLine($"{node.MethodSpecifier.Name};");
        }

        public void PureTranslateTypeOfNode(TypeOfNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.TypePin)} = typeof({node.InputTypePin.InferredType?.Value?.FullCodeNameUnbound ?? "System.Object"});");
        }

        public void PureTranslateMakeArrayNode(MakeArrayNode node)
        {
            builder.Append($"{GetOrCreatePinName(node.OutputDataPins[0])} = new {node.ArrayType.FullCodeName}");

            // Use predefined size or initializer list
            if (node.UsePredefinedSize)
            {
                // HACKish: Remove trailing "[]" contained in type
                builder.Remove(builder.Length - 2, 2);
                builder.AppendLine($"[{GetPinIncomingValue(node.SizePin)}];");
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine("{");

                foreach (var inputDataPin in node.InputDataPins)
                {
                    builder.AppendLine($"{GetPinIncomingValue(inputDataPin)},");
                }

                builder.AppendLine("};");
            }
        }
        public void PureTranslateDefaultNode(DefaultNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.DefaultValuePin)} = default({node.Type.FullCodeName});");
        }

        public void TranslateRerouteNode(RerouteNode node)
        {
            if (node.ExecRerouteCount + node.TypeRerouteCount + node.DataRerouteCount != 1)
            {
                throw new NotImplementedException("Only implemented reroute nodes with exactly 1 type of pin.");
            }

            if (node.DataRerouteCount == 1)
            {
                builder.AppendLine($"{GetOrCreatePinName(node.OutputDataPins[0])} = {GetPinIncomingValue(node.InputDataPins[0])};");
            }
            else if (node.ExecRerouteCount == 1)
            {
                WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
            }
        }

        public void EmitControlFlowEnd(NodeInputExecPin nodeInputExecPin)
        {
            if (this.graph is MethodGraph {IsCoroutine: true})
            {
                builder.AppendLine("yield break;");
            }
            else
            {
                // Only write return if the return node is not the last node
                if (GetExecPinStateId(nodeInputExecPin) != nodeStateIds.Count - 1)
                {
                    builder.AppendLine("return;");

                }
            }
        }
    }
}
