using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace PhotoForge.Installer;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var args = e.Args.Select(a => a.Trim().ToLowerInvariant()).ToArray();
        bool silent = args.Contains("/s") || args.Contains("/quiet") || args.Contains("--silent");

        if (silent)
        {
            try
            {
                var targetDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "PhotoForge");

                InstallerEngine.PerformInstall(targetDir, createShortcuts: true, registerShell: true, addToPath: true);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "photoforge_install_error.log"), ex.ToString());
                Environment.Exit(1);
            }
        }
        else
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
