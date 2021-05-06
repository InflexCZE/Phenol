using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ExposedObject;
using HarmonyLib;
using LiveLink;
using LiveLink.Messages;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage;
using VRage.FileSystem;
using VRage.Game;
using VRage.Plugins;
using VRage.Scripting;

public class __HIT__
{
    public static long ActiveDebugTarget = -1;

    [ThreadStatic]
    private static Stack<long> _RunningDebugTargetStack;

    private static Stack<long> RunningDebugTargetStack
    {
        get
        {
            var targets = _RunningDebugTargetStack;
            
            if(targets == null)
            {
                targets = new Stack<long>();
                _RunningDebugTargetStack = targets;
            }

            return targets;
        }
    }

    public static void Hit(int @class, int method, int nodeIndex, int pinIndex)
    {
        if(ActiveDebugTarget == -1)
            return;

        if(RunningDebugTargetStack.Count == 0 || RunningDebugTargetStack.Peek() != ActiveDebugTarget)
        {
            return;
        }

        var hitPoints = SpaceLink.SpaceLink.Instance.HitPoints;
        lock(hitPoints)
        {
            hitPoints.Add(new HitPoint(@class, method, nodeIndex, pinIndex));
        }
    }

    public static void PushRunningDebugTarget(long debugTarget)
    {
        RunningDebugTargetStack.Push(debugTarget);
    }
    
    public static void PopDebugTarget(long? expected)
    {
        if(RunningDebugTargetStack.Count == 0)
        {
            Debug.Fail("No active debug targets");
            return;
        }

        var active = RunningDebugTargetStack.Pop();
        if(expected.HasValue)
        {
            Debug.Assert(active == expected, "Debug target mismatch");
        }
    }
}

namespace SpaceLink
{
    public class SpaceLink : IConfigurablePlugin
    {
        public static SpaceLink Instance;

        public Harmony Harmony;
        public Connection Connection { get; private set; }
        private readonly ConcurrentQueue<Action> InvocationQueue = new();

        public HashSet<HitPoint> HitPoints = new();
        public List<(string DisplayName, int ModId)> ActiveMods = new();

        public void Init(object gameInstance)
        {
            Instance = this;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            this.Harmony = new Harmony(nameof(SpaceLink));
            this.Harmony.PatchAll();

            PatchWhitelist();
            MySession.BeforeLoading += MySession_BeforeLoading;
        }

        private void MySession_BeforeLoading()
        {
            this.ActiveMods.Clear();
        }

        public void OpenConnection()
        {
            var connection = this.Connection = new Connection(true);
            this.Connection.RegisterMessageHandler<ListDebugTargets>(request =>
            {
                Invoke(() =>
                {
                    var entities = Exposed.From(typeof(MyEntityIdentifier));
                    IEnumerable<object> worldEntities = entities.EntityList.Values;

                    var blocks = worldEntities.OfType<MyProgrammableBlock>().Select(x =>
                    {
                        return new DebugTargets.DebugTarget
                        {
                            Id = x.EntityId,
                            Name = $"{x.CubeGrid.DisplayName} - {x.DisplayNameText}"
                        };
                    });

                    var mods = this.ActiveMods.Select(x =>
                    {
                        return new DebugTargets.DebugTarget
                        {
                            Id = x.ModId,
                            Name = x.DisplayName
                        };
                    });

                    this.Connection?.Send(new DebugTargets
                    {
                        Request = request.MsgId,
                        Targets = blocks.Concat(mods).ToList()
                    });
                });

                return true;
            });

            this.Connection.RegisterMessageHandler<DeployScript>(message =>
            {
                Invoke(() =>
                {
                    if(MyEntities.TryGetEntityById<MyProgrammableBlock>(message.DebugTarget, out var pb))
                    {
                        //Note: Automatically recompiles
                        ((IMyProgrammableBlock)pb).ProgramData = message.Code;
                    }
                });
                return true;
            });
            
            this.Connection.RegisterMessageHandler<HitpointsRequest>(message =>
            {
                __HIT__.ActiveDebugTarget = message.Target;
                return true;
            });

            this.Connection.OnConnectionLost += () =>
            {
                __HIT__.ActiveDebugTarget = -1;
                Invoke(() =>
                {
                    connection.Dispose();
                    this.Connection = null;
                });
            };
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if(args.Name.Contains("SpaceLink"))
            {
                return Assembly.GetExecutingAssembly();
            }

            return null;
        }

#if DEBUG
        private bool startSession = true;
#else
        private bool startSession = false;
#endif

        public void Update()
        {
            if(startSession)
            {
                startSession = false;
                var screenT = AccessTools.TypeByName("Sandbox.Game.Gui.MyGuiScreenStartQuickLaunch");
                var screen = Activator.CreateInstance(screenT, MyQuickLaunchType.LAST_SANDBOX, MyCommonTexts.StartGameInProgressPleaseWait);
                MyGuiSandbox.AddScreen((MyGuiScreenBase)screen);
            }

            while(this.InvocationQueue.TryDequeue(out var action))
            {
                action.Invoke();
            }

            if(this.Connection == null)
            {
                OpenConnection();
            }

            if(this.HitPoints.Count > 0)
            {
                this.Connection.Send(new HitPoints
                {
                    Ids = this.HitPoints.ToList(),
                    DebugTarget = __HIT__.ActiveDebugTarget,
                });
            }

            this.HitPoints.Clear();
        }

        public string GetPluginTitle()
        {
            return nameof(SpaceLink);
        }

        public IPluginConfiguration GetConfiguration(string userDataPath)
        {
            return new Config();
        }

        public class Config : IPluginConfiguration
        {
            public void Save(string userDataPath)
            { }
        }

        private static void PatchWhitelist()
        {
            MyScriptCompiler.Static.AddReferencedAssemblies(Assembly.GetExecutingAssembly().Location);
            MyScriptCompiler.Static.DiagnosticOutputPath = Path.Combine(MyFileSystem.UserDataPath, "ScriptDiagnostics");

            var whitelistT = typeof(MyScriptWhitelist);
            var whitelistTargetT = typeof(MyWhitelistTarget);
            var symbolT = AccessTools.TypeByName("Microsoft.CodeAnalysis.ISymbol");
            MyScriptCompiler.Static.AddConditionalCompilationSymbols("__SPACE_LINK__");
            
            var original = AccessTools.Method(whitelistT, "IsWhitelisted", new[] { symbolT, whitelistTargetT });
            var redirectionTarget = ((Func<object, int, bool>) IsWhitelisted).Method;
            RedirectMethod(original, redirectionTarget);

            static bool IsWhitelisted(object symbol, int whitelistTarget)
            {
                return true;
            }

            static void RedirectMethod(MethodBase original, MethodBase redirectionTarget)
            {
                var error = Memory.DetourMethod(original, redirectionTarget);

                if (string.IsNullOrEmpty(error) == false)
                {
                    throw new Exception(error);
                }
            }
        }

        [HarmonyPatch]
        public static class CurrentlyRunningPBPatch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MyProgrammableBlock), "RunSandboxedProgramActionCore") ?? 
                       AccessTools.Method(typeof(MyProgrammableBlock), "RunSandboxedProgramAction");
            }

            public static void Prefix(MyProgrammableBlock __instance)
            {
                __HIT__.PushRunningDebugTarget(__instance.EntityId);
            }

            public static void Postfix(MyProgrammableBlock __instance)
            {
                __HIT__.PopDebugTarget(__instance.EntityId);
            }
        }

        [HarmonyPatch]
        public static class ModIdCollectorPatch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MyModWatchdog), "AllocateModId");
            }

            public static void Postfix(string modName, int __result)
            {
                var modId = __result;
                var activeMods = Instance.ActiveMods;
                lock(activeMods)
                {
                    activeMods.Add((modName, modId));
                }
            }
        }

        [HarmonyPatch]
        public static class CurrentlyRunningModPatch1
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MyModWatchdog), "ModMethodEnter");
            }

            public static void Prefix(int modId)
            {
                __HIT__.PushRunningDebugTarget(modId);
            }
        }

        [HarmonyPatch]
        public static class CurrentlyRunningModPatch2
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MyModWatchdog), "ModMethodExit");
            }

            public static void Prefix()
            {
                __HIT__.PopDebugTarget(null);
            }
        }

        private void Invoke(Action action)
        {
            this.InvocationQueue.Enqueue(action);
        }

        public void Dispose()
        {
            this.Connection.Dispose();
        }
    }
}
