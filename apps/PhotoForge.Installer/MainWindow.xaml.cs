using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace PhotoForge.Installer;

public partial class MainWindow : Window
{
    private string _defaultTargetDir;

    public MainWindow()
    {
        InitializeComponent();
        _defaultTargetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "PhotoForge");
        TxtTargetDir.Text = _defaultTargetDir;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select PhotoForge Installation Folder",
            InitialDirectory = _defaultTargetDir
        };

        if (dialog.ShowDialog() == true)
        {
            TxtTargetDir.Text = dialog.FolderName;
        }
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        if (StepFinished.Visibility == Visibility.Visible)
        {
            if (ChkLaunch.IsChecked == true)
            {
                var desktopExe = Path.Combine(TxtTargetDir.Text, "PhotoForge.Desktop.exe");
                if (File.Exists(desktopExe))
                {
                    Process.Start(new ProcessStartInfo(desktopExe) { UseShellExecute = true });
                }
            }
            Close();
            return;
        }

        var targetDir = TxtTargetDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("Please specify a valid installation folder.", "Installation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StepOptions.Visibility = Visibility.Collapsed;
        StepProgress.Visibility = Visibility.Visible;
        BtnCancel.IsEnabled = false;
        BtnInstall.IsEnabled = false;

        bool createShortcuts = ChkShortcuts.IsChecked == true;
        bool registerShell = ChkShell.IsChecked == true;
        bool addToPath = ChkPath.IsChecked == true;

        try
        {
            await Task.Run(() =>
            {
                InstallerEngine.PerformInstall(
                    targetDir,
                    createShortcuts,
                    registerShell,
                    addToPath,
                    (msg, progress) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TxtStatus.Text = msg;
                            PrgInstall.Value = progress;
                        });
                    });
            });

            StepProgress.Visibility = Visibility.Collapsed;
            StepFinished.Visibility = Visibility.Visible;
            BtnInstall.Content = "Finish";
            BtnInstall.IsEnabled = true;
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StepProgress.Visibility = Visibility.Collapsed;
            StepOptions.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;
            BtnInstall.IsEnabled = true;
            MessageBox.Show($"Installation failed: {ex.Message}", "Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
