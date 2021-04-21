using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExposedObject;
using HarmonyLib;
using LiveLink.Connection;
using LiveLink.Messages;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Gui;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage;
using VRage.FileSystem;
using VRage.Game;
using VRage.Plugins;
using VRage.Scripting;

public class __HIT__
{
    public static long ActivePB;
    public static long CurrentlyRunningPB;

    public static void Hit(int @class, int method, int nodeIndex, int pinIndex)
    {
        if(CurrentlyRunningPB != ActivePB)
            return;

        var spaceLink = SpaceLink.SpaceLink.Instance;
        spaceLink.HitPoints.Add(new HitPoint(@class, method, nodeIndex, pinIndex));
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

        public HashSet<HitPoint> HitPoints = new ();

        public void Init(object gameInstance)
        {
            Instance = this;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            this.Harmony = new Harmony(nameof(SpaceLink));
            this.Harmony.PatchAll();

            PatchWhitelist();
        }

        public void OpenConnection()
        {
            var connection = this.Connection = new Connection(Side.In);
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
                            Name = $"{x.CubeGrid.DisplayName} - {x.DisplayName}"
                        };
                    }).ToList();

                    this.Connection.Send(new DebugTargets
                    {
                        Targets = blocks,
                        Request = request.MsgId
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
                __HIT__.ActivePB = message.Target;
                return true;
            });

            this.Connection.OnConnectionLost += () =>
            {
                __HIT__.ActivePB = -1;
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
                    DebugTarget = __HIT__.ActivePB,
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
                __HIT__.CurrentlyRunningPB = __instance.EntityId;
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
