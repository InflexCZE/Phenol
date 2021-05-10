using NetPrints.Core;
using NetPrints.Graph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private readonly Dictionary<NodeOutputDataPin, string> variableNames = new();
        private readonly Dictionary<Node, int[]> emittedNodes = new();
        
        private int LabelCounter = 0;

        private readonly StringBuilder builder = new();

        private ExecutionGraph graph;

        private Random random;

        private delegate void NodeTypeHandler(ExecutionGraphTranslator translator, Node node);

        private static readonly Dictionary<Type, NodeTypeHandler> nodeTypeHandlers = new()
        {
            SimpleNode<ReturnNode>((x, y) => x.TranslateReturnNode(y)),
            RawNode<MethodEntryNode>((x, y) => x.TranslateExecutionEntry(y)),
            RawNode<ConstructorEntryNode>((x, y) => x.TranslateExecutionEntry(y)),
            
            NodeWithInputs<IfElseNode>((x, y) => x.TranslateIfElseNode(y)),
            RawNode<CallMethodNode>((x, y) => x.TranslateCallMethodNode(y)),
            NodeWithInputs<ForLoopNode>((x, y) => x.TranslateForLoopNode(y)),
            NodeWithInputs<SequenceNode>((x, y) => x.TranslateSequenceNode(y)),
            SimpleNode<ConstructorNode>((x, y) => x.TranslateConstructorNode(y)),
            NodeWithInputs<ForeachLoopNode>((x, y) => x.TranslateForeachLoopNode(y)),

            SimpleNode<RerouteNode>((x, y) => x.TranslateRerouteNode(y)),
            SimpleNode<VariableSetterNode>((x, y) => x.TranslateVariableSetterNode(y)),
            SimpleNode<VariableGetterNode>((x, y) => x.TranslateVariableGetterNode(y)),
            
            SimpleNode<TypeOfNode>((x, y) => x.TranslateTypeOfNode(y)),
            SimpleNode<TernaryNode>((x, y) => x.TranslateTernaryNode(y)),
            SimpleNode<LiteralNode>((x, y) => x.TranslateLiteralNode(y)),
            SimpleNode<DefaultNode>((x, y) => x.TranslateDefaultNode(y)),
            SimpleNode<MakeArrayNode>((x, y) => x.TranslateMakeArrayNode(y)),
            NodeWithInputs<SelectValueNode>((x, y) => x.TranslateSelectValueNode(y)),
            SimpleNode<MakeDelegateNode>((x, y) => x.TranslateMakeDelegateNode(y)),
            NodeWithInputs<ExplicitCastNode>((x, y) => x.TranslateExplicitCastNode(y)),
            
            SimpleNode<ThrowNode>((x, y) => x.TranslateThrowNode(y)),
            SimpleNode<DelayNode>((x, y) => x.TranslateDelayNode(y)),
            SimpleNode<AwaitNode>((x, y) => x.TranslateAwaitNode(y)),
        };

        private int GetNextLabelId()
        {
            return LabelCounter++;
        }

        private int GetExecPinStateId(NodeInputExecPin pin)
        {
            return emittedNodes[pin.Node][pin.Node.InputExecPins.IndexOf(pin)];
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

        private bool TryGetConstantValue<T>(NodeInputDataPin pin, out T value)
        {
            try
            {
                var data = GetPinIncomingValue(pin);
                value = (T) Convert.ChangeType(data, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            { }

            value = default;
            return false;
        }

        private string GetPinIncomingValue(NodeInputDataPin pin)
        {
            if (pin.IncomingPin == null)
            {
                return TranslatorUtil.GetUnconnectedValue(pin);
            }

            return GetOrCreatePinName(pin.IncomingPin);
        }

        private string GetPinIncomingValueOrDefault(NodeInputDataPin pin)
        {
            try
            {
                return GetPinIncomingValue(pin);
            }
            catch
            {
                return $"default({pin.PinType.Value.FullCodeName})";
            }
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

        private void CreateVariables()
        {
            foreach(var node in TranslatorUtil.GetAllNodesInExecGraph(graph))
            {
                GetOrCreatePinNames(node.OutputDataPins);
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

            builder.AppendLine(TranslatorUtil.TranslateAttributes(graph.DefinedAttributes));

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
            this.ControlFlowEndStack.Clear();
            variableNames.Clear();
            emittedNodes.Clear();
            LabelCounter = 0;
            builder.Clear();
            random = new Random(0);

            // Create variables for all output pins for every node
            CreateVariables();

            // Write the signatures
            if (withSignature)
            {
                TranslateSignature();
            }

            builder.AppendLine("{"); // Method start

            // Write the variable declarations
            TranslateVariables();
            builder.AppendLine();

            // Translate every exec node
            TranslateNode(graph.EntryNode);

            builder.AppendLine("}"); // Method end

            string code = builder.ToString();

            // Remove unused labels
            return RemoveUnnecessaryLabels(code);
        }

        private void TranslateNode(NodeOutputExecPin source)
        {
            if(source.OutgoingPin is null)
                return;

            var targetPin = source.OutgoingPin;
            var targetNode = targetPin.Node;
            var sourceNode = source.Node;

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
            
            var nodeIndex = method.Nodes.IndexOf(sourceNode);
            if(nodeIndex >= 0 && lifeLinkFormat is not null)
            {
                var pinIndex = sourceNode.OutputExecPins.IndexOf(source);
                builder.AppendLine(string.Format(lifeLinkFormat, classIndex, methodIndex, nodeIndex, pinIndex));
            }

            if(emittedNodes.ContainsKey(targetNode))
            {
                WriteGotoInputPin(targetPin);
            }
            else
            {
                var jumpTable = Enumerable.Range(0, targetNode.InputExecPins.Count).Select(_ => GetNextLabelId()).ToArray();
                emittedNodes.Add(targetNode, jumpTable);

                builder.AppendLine($"State{emittedNodes[targetNode][0]}:");
                TranslateNode(targetNode);
                builder.AppendLine();

                emittedNodes.Remove(targetNode);
            }
        }

        private string RemoveUnnecessaryLabels(string code)
        {
            foreach (int stateId in Enumerable.Range(0, LabelCounter))
            {
                if (!code.Contains($"goto State{stateId};"))
                {
                    code = code.Replace($"State{stateId}:", "");
                }
            }

            return code;
        }

        public void TranslateNode(Node node)
        {
            if (!(node is RerouteNode))
            {
                builder.AppendLine($"// {node}");
            }

            if (nodeTypeHandlers.TryGetValue(node.GetType(), out var handlers))
            {
                handlers(this, node);
            }
            else
            {
                Debug.WriteLine($"Unhandled type {node.GetType()} in TranslateNode");
            }
        }

        private void WriteGotoInputPin(NodeInputExecPin pin)
        {
            builder.AppendLine($"goto State{GetExecPinStateId(pin)};");
        }
        
        public void TranslateDependentPureNodes(Node node)
        {
            var sortedPureNodes = TranslatorUtil.GetSortedPureNodes(node);
            foreach(Node depNode in sortedPureNodes)
            {
                TranslateNode(depNode);
            }
        }

        private void EmitBlockFromPin(NodeOutputExecPin pin, string enterCondition = null)
        {
            builder.AppendLine($"while({enterCondition ?? "true"})");
            builder.AppendLine("{");
            FollowExecutionChain(pin, "break");
            builder.AppendLine("}");
        }

        public void FollowExecutionChain(NodeOutputExecPin pin, string flowEnd = null)
        {
            var hasFlowEnd = flowEnd is not null;
            
            FlowToken flowToken = default;
            if(hasFlowEnd)
            {
                flowToken = PushControlFlowEnd(flowEnd);
            }

            if(pin.OutgoingPin != null)
            {
                TranslateNode(pin);
            }
            else
            {
                EmitControlFlowEnd();
            }

            if(hasFlowEnd)
            {
                flowToken.Dispose();
            }
        }

        public void TranslateExecutionEntry(ExecutionEntryNode node)
        {
            var isCoroutine = this.graph is MethodGraph { IsCoroutine: true };
            var returnStatement = isCoroutine ? "yield break" : "return";
            FollowExecutionChain(node.OutputExecPins[0], returnStatement);

            Debug.Assert(ControlFlowEndStack.Count == 0);
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
                FollowExecutionChain(node.OutputExecPins[0]);
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
                    FollowExecutionChain(node.CatchPin);
                }

                builder.AppendLine("}");
            }
        }

        public void TranslateConstructorNode(ConstructorNode node)
        {
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
        }

        public void TranslateExplicitCastNode(ExplicitCastNode node)
        {
            var inputValue = GetPinIncomingValue(node.ObjectToCast);
            var castTypeName = node.CastType.FullCodeNameUnbound;
            var outputValue = GetOrCreatePinName(node.CastPin);


            // If failure pin is not connected just throw, don't check.
            // Otherwise check if cast can be performed and choose appropriate path
            bool safeCast = node.IsPure == false && (node.CastSuccessPin.IsConnected || node.CastFailedPin.IsConnected);
            if (safeCast)
            {
                builder.AppendLine($"if(!({inputValue} is {castTypeName}))");
                builder.AppendLine("{");
                FollowExecutionChain(node.CastFailedPin);
                builder.AppendLine("}");
                builder.AppendLine("else");
                builder.AppendLine("{");
            }

            builder.AppendLine($"{outputValue} = ({castTypeName}){inputValue};");

            if(node.IsPure == false)
            {
                FollowExecutionChain(node.CastSuccessPin);
            }

            if(safeCast)
            {
                builder.AppendLine("}");
            }
        }

        public void TranslateThrowNode(ThrowNode node)
        {
            builder.AppendLine($"throw {GetPinIncomingValue(node.ExceptionPin)};");
        }

        public void TranslateAwaitNode(AwaitNode node)
        {
            // Store result if task has a return value.
            if (node.ResultPin != null)
            {
                builder.Append($"{GetOrCreatePinName(node.ResultPin)} = ");
            }

            builder.AppendLine($"await {GetPinIncomingValue(node.TaskPin)};");
        }

        public void TranslateTernaryNode(TernaryNode node)
        {
            builder.Append($"{GetOrCreatePinName(node.OutputObjectPin)} = ");
            builder.Append($"{GetPinIncomingValue(node.ConditionPin)} ? ");
            builder.Append($"{GetPinIncomingValue(node.TrueObjectPin)} : ");
            builder.AppendLine($"{GetPinIncomingValue(node.FalseObjectPin)};");
        }

        public void TranslateVariableSetterNode(VariableSetterNode node)
        {
            string valueName = GetPinIncomingValue(node.NewValuePin);

            var acessor = string.Empty;

            // Add target name if there is a target (null for local and static variables)
            if (node.IsStatic)
            {
                if (!(node.TargetType is null))
                {
                    acessor = node.TargetType.FullCodeName;
                }
                else
                {
                    acessor = node.Graph.Class.Name;
                }
            }
            
            if (node.TargetPin != null)
            {
                if (node.TargetPin.IncomingPin != null)
                {
                    acessor += GetOrCreatePinName(node.TargetPin.IncomingPin);
                }
                else
                {
                    acessor += "this";
                }
            }

            if (node.IsIndexer)
            {
                 acessor += $"[{GetPinIncomingValue(node.IndexPin)}]";
            }
            else
            {
                acessor += $".{node.VariableName}";
            }

            if(node.IsEvent)
            {
                var emitConditions = true;
                var emitSubscription = true;
                var emitUnsubscription = true;

                if(TryGetConstantValue(node.SubscribePin, out bool subscribe))
                {
                    emitConditions = false;
                    emitSubscription = subscribe;
                    emitUnsubscription = subscribe == false;
                }

                if(emitConditions)
                {
                    builder.AppendLine($"if({GetPinIncomingValue(node.SubscribePin)})");
                    builder.AppendLine("{");
                }

                if(emitSubscription)
                {
                    builder.AppendLine($"{acessor} += {valueName};");
                }

                if(emitConditions)
                {
                    builder.AppendLine("}");
                    builder.AppendLine("else");
                    builder.AppendLine("{");
                }

                if(emitUnsubscription)
                {
                    builder.AppendLine($"{acessor} -= {valueName};");
                }

                if(emitConditions)
                {
                    builder.AppendLine("}");
                }
            }
            else
            {
                builder.AppendLine($"{acessor} = {valueName};");
            }

            // Set output pin of this node to the same value
            builder.AppendLine($"{GetOrCreatePinName(node.OutputDataPins[0])} = {valueName};");
        }

        public void TranslateDelayNode(DelayNode node)
        {
            builder.AppendLine("yield return null;");
        }

        public void TranslateReturnNode(ReturnNode node)
        {
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
                EmitControlFlowEnd(fullReturn: true);
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
            string conditionVar = GetPinIncomingValue(node.ConditionPin);

            builder.AppendLine($"if ({conditionVar})");
            builder.AppendLine("{");
            FollowExecutionChain(node.TruePin);
            builder.AppendLine("}");

            builder.AppendLine("else");
            builder.AppendLine("{");
            FollowExecutionChain(node.FalsePin);
            builder.AppendLine("}");
        }

        public void TranslateForLoopNode(ForLoopNode node)
        {
            var index = GetOrCreatePinName(node.IndexPin);
            builder.AppendLine($"for({index} = {GetPinIncomingValue(node.InitialIndexPin)}; {index} < {GetPinIncomingValue(node.MaxIndexPin)}; {index}++)");
            builder.AppendLine("{");
            {
                FollowExecutionChain(node.LoopPin, "continue");
            }
            builder.AppendLine("}");

            FollowExecutionChain(node.CompletedPin);
        }

        public void TranslateForeachLoopNode(ForeachLoopNode node)
        {
            var elementName = GetOrCreatePinName(node.DataPin);
            var indexName = GetOrCreatePinName(node.IndexPin);
            var tmpElementName = $"__{elementName}_tmp__";

            builder.AppendLine($"{indexName} = -1;");
            builder.AppendLine($"foreach(var {tmpElementName} in {GetOrCreatePinName(node.DataCollectionPin.IncomingPin)})");
            builder.AppendLine("{");

            builder.AppendLine($"{indexName}++;");
            builder.AppendLine($"{elementName} = {tmpElementName};");
            {
                FollowExecutionChain(node.LoopPin, "continue");
            }
            builder.AppendLine("}");
            
            FollowExecutionChain(node.CompletedPin);
        }

        public void TranslateVariableGetterNode(VariableGetterNode node)
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

        public void TranslateLiteralNode(LiteralNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.ValuePin)} = {GetPinIncomingValue(node.InputDataPins[0])};");
        }

        public void TranslateMakeDelegateNode(MakeDelegateNode node)
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

        public void TranslateTypeOfNode(TypeOfNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.TypePin)} = typeof({node.InputTypePin.InferredType?.Value?.FullCodeNameUnbound ?? "System.Object"});");
        }

        public void TranslateMakeArrayNode(MakeArrayNode node)
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
        public void TranslateDefaultNode(DefaultNode node)
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
        }

        public void TranslateSelectValueNode(SelectValueNode node)
        {
            var value = GetOrCreatePinName(node.OutputValuePin);

            foreach(var (branch, condition) in node.Conditionals)
            {
                builder.AppendLine($" if({GetPinIncomingValue(condition)})");
                builder.AppendLine("{");
                builder.AppendLine($"{value} = {GetPinIncomingValue(branch)};");
                builder.AppendLine("}");
                builder.Append("else");
            }

            builder.AppendLine();
            builder.AppendLine("{");
            builder.AppendLine($"{value} = {GetPinIncomingValueOrDefault(node.DefaultValuePin)};");

            if(node.IsPure == false)
            {
                FollowExecutionChain(node.DefaultMatchExecPin);
            }

            builder.AppendLine("}");

            if(node.IsPure == false)
            {
                FollowExecutionChain(node.MatchExecPin);
            }
        }
        
        public void TranslateSequenceNode(SequenceNode node)
        {
            foreach(var (branch, condition) in node.Branches.Zip(node.Conditions))
            {
                if(branch.OutgoingPin == null)
                    continue;

                EmitBlockFromPin(branch, GetPinIncomingValue(condition));
            }
            
            FollowExecutionChain(node.AlwaysPin);
        }

        private static KeyValuePair<Type, NodeTypeHandler> RawNode<TNode>(Action<ExecutionGraphTranslator, TNode> nodeHandler) where TNode : Node
        {
            return new (typeof(TNode), (translator, node) => nodeHandler(translator, (TNode) node));
        }
        
        private static KeyValuePair<Type, NodeTypeHandler> SimpleNode<TNode>(Action<ExecutionGraphTranslator, TNode> nodeHandler) where TNode : Node
        {
            return NodeWithInputs<TNode>((translator, node) =>
            {
                nodeHandler(translator, node);
                
                if(node.IsPure == false)
                {
                    Debug.Assert(node.OutputExecPins.Count <= 1, "Multi-output node is not expected");
                    foreach(var outputPin in node.OutputExecPins)
                    {
                        translator.FollowExecutionChain(outputPin);
                    }
                }
            });
        }
        
        private static KeyValuePair<Type, NodeTypeHandler> NodeWithInputs<TNode>(Action<ExecutionGraphTranslator, TNode> nodeHandler) where TNode : Node
        {
            return new(typeof(TNode), (translator, node) =>
            {
                if(node.IsPure == false)
                {
                    translator.TranslateDependentPureNodes(node);
                }
                
                nodeHandler(translator, (TNode) node);
            });
        }

        private List<string> ControlFlowEndStack = new ();

        private struct FlowToken : IDisposable
        {
            private List<string> ControlFlowEndStack;

            public FlowToken(List<string> controlFlowEndStack)
            {
                //this.Builder = builder;
                //controlFlowEndStack.Add(flowEnd + ';' + Environment.NewLine);
                this.ControlFlowEndStack = controlFlowEndStack;
            }

            public void Dispose()
            {
                //var builderLength = this.Builder.Length;
                //var flowEnd = this.ControlFlowEndStack.Last();
                //
                ////If flow end is the last thing in the function, we can safely remove it to suppress warning
                //if(builderLength >= flowEnd.Length)
                //{
                //    int begin = builderLength - flowEnd.Length;
                //    var ending = this.Builder.ToString(begin, flowEnd.Length);
                //    if(ending == flowEnd)
                //    {
                //        this.Builder.Remove()
                //    }
                //}

                this.ControlFlowEndStack.RemoveAt(this.ControlFlowEndStack.Count - 1);
            }

        }

        private FlowToken PushControlFlowEnd(string controlFlowEnd)
        {
            this.ControlFlowEndStack.Add(controlFlowEnd);
            return new FlowToken(this.ControlFlowEndStack);
        }

        public void EmitControlFlowEnd(bool fullReturn = false)
        {
            var controlFlowEnd = fullReturn ? this.ControlFlowEndStack[0] : this.ControlFlowEndStack.Last();
            builder.Append(controlFlowEnd).Append(';').AppendLine();
        }
    }
}
