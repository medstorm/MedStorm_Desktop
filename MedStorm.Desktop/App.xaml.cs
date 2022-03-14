using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MedStorm.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Log.Information("MedStorm.Desktop exit");
            Log.CloseAndFlush();
        }

        protected override void OnStartup(StartupEventArgs e)
        {

            base.OnStartup(e);

            // Catch exceptions from all threads in the AppDomain.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            // Catch exceptions from a single specific UI dispatcher thread.
            DispatcherUnhandledException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
                e.Handled = true;
            };

            // Catch exceptions from each AppDomain that uses a task scheduler for async operations.
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            try
            {
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                Log.Error($"LogUnhandledException: Unhandled exception in {assemblyName.Name} version={assemblyName.Version}");
                Log.Error($"LogUnhandledException: StrackTrace={exception.StackTrace}");
            }
            catch (Exception ex)
            {
                Log.Error($"App.LogUnhandledException: Exception in exception-handler Source={source}, Error={ex.Message}");
            }
        }

    }
}
