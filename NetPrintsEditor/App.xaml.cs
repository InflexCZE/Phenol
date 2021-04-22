using System;
using NetPrints.Core;
using NetPrintsEditor.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace NetPrintsEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string[] StartupArguments
        {
            get;
            private set;
        }

        public static IReflectionProvider ReflectionProvider
        {
            get;
            private set;
        }

        public static ObservableRangeCollection<TypeSpecifier> NonStaticTypes
        {
            get;
        } = new ObservableRangeCollection<TypeSpecifier>();

        public static void ReloadReflectionProvider(IEnumerable<string> assemblyPaths, IEnumerable<string> sourcePaths, IEnumerable<string> sources)
        {
            ReflectionProvider = new MemoizedReflectionProvider(new ReflectionProvider(assemblyPaths, sourcePaths, sources));

            // Cache static types.
            // Needs to be done on UI thread since it is an observable collection to
            // which we bind.
            Current.Dispatcher.Invoke(() => NonStaticTypes.ReplaceRange(ReflectionProvider.GetNonStaticTypes()));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            StartupArguments = e.Args;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            base.OnStartup(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if(Directory.Exists(Environment.CurrentDirectory) && e.ExceptionObject is Exception exception)
            {
                var crashLog = Path.Combine(Environment.CurrentDirectory, "CrashLog.txt");
                File.WriteAllText(crashLog, exception + Environment.NewLine + Environment.NewLine + exception.StackTrace);
            }
        }
    }
}
