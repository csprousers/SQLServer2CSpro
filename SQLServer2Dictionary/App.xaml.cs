using SqlConnectionDialog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SQLServer2Dictionary
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //Disable shutdown when the dialog closes
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var factory = new ConnectionStringFactory();
            var connectionString = factory.BuildConnectionString();

            if (String.IsNullOrEmpty(connectionString))
            {
                // Cancelled
                Current.Shutdown(-1);
            }
            else
            {
                var mainWindow = new MainWindow(connectionString);
                //Re-enable normal shutdown mode.
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
        }
    }
}
