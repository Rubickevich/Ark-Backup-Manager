using System;
using System.Windows;

namespace Backup
{
    /*
     My attempt at handling the exceptions globally. Unsure of how effective it actually is, since most errors require an individual course
     of actions to be resolved.
     */
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Instance?.Log($"Unhandled UI exception: {e.Exception.Message}", LogType.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Instance?.Log($"Unhandled domain exception: {ex.Message}", LogType.Error);
            }
        }
    }
}