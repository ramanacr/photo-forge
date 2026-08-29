using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PhotoForge.Installer;

public partial class MainWindow : Window
{
    private string _defaultTargetDir;
    private readonly List<BitmapImage> _slides = new();
    private int _currentSlideIndex = 0;
    private DispatcherTimer? _slideTimer;

    public MainWindow()
    {
        InitializeComponent();
        _defaultTargetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "PhotoForge");
        TxtTargetDir.Text = _defaultTargetDir;

        LoadSlides();
    }

    private void LoadSlides()
    {
        string[] slideNames = ["slide1.jpg", "slide2.jpg", "slide3.jpg", "slide4.jpg"];
        foreach (var name in slideNames)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Resources/Slides/{name}", UriKind.Absolute);
                var bmp = new BitmapImage(uri);
                bmp.Freeze();
                _slides.Add(bmp);
            }
            catch { }
        }

        if (_slides.Count > 0)
        {
            ImgSlide.Source = _slides[0];
            UpdateSlideIndicator(0);
        }
    }

    private void StartSlideRotation()
    {
        if (_slides.Count <= 1) return;

        _slideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _slideTimer.Tick += (s, e) =>
        {
            _currentSlideIndex = (_currentSlideIndex + 1) % _slides.Count;
            ImgSlide.Source = _slides[_currentSlideIndex];
            UpdateSlideIndicator(_currentSlideIndex);
        };
        _slideTimer.Start();
    }

    private void StopSlideRotation()
    {
        _slideTimer?.Stop();
        _slideTimer = null;
    }

    private void UpdateSlideIndicator(int activeIndex)
    {
        var dots = new string[Math.Max(1, _slides.Count)];
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i] = (i == activeIndex) ? "●" : "○";
        }
        TxtSlideIndicator.Text = string.Join(" ", dots);
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

        StartSlideRotation();

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

            StopSlideRotation();
            StepProgress.Visibility = Visibility.Collapsed;
            StepFinished.Visibility = Visibility.Visible;
            BtnInstall.Content = "Finish";
            BtnInstall.IsEnabled = true;
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StopSlideRotation();
            StepProgress.Visibility = Visibility.Collapsed;
            StepOptions.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;
            BtnInstall.IsEnabled = true;
            MessageBox.Show($"Installation failed: {ex.Message}", "Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        StopSlideRotation();
        Close();
    }
}
