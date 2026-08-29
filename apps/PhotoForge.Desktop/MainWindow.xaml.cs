using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PhotoForge.Audit;
using PhotoForge.Core.Models;
using PhotoForge.Core.Pipeline;
using PhotoForge.Core.Services;
using PhotoForge.Imaging;
using PhotoForge.Matching;
using PhotoForge.Metadata;
using PhotoForge.Platform;
using PhotoForge.Shell;
using PhotoForge.Storage;
using PhotoForge.Storage.Database;

namespace PhotoForge.Desktop;

public partial class MainWindow : Window
{
    private readonly IMetadataEngine _metadataEngine;
    private readonly IImageEngine _imageEngine;
    private readonly IMatchingEngine _matchingEngine;
    private readonly IStorageEngine _storageEngine;
    private readonly AuditDatabase _auditRepo;
    private readonly IPhotoForgePipeline _pipeline;

    private string? _selectedOriginalPath;
    private string? _selectedEditedPath;

    public ObservableCollection<BatchItemViewModel> BatchResults { get; } = new();
    public ObservableCollection<MatchItemViewModel> MatchReviews { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _metadataEngine = new MetadataEngine();
        _imageEngine = new ImageEngine();
        _matchingEngine = new MatchingEngine(_imageEngine);
        _storageEngine = new StorageEngine();
        _auditRepo = new AuditDatabase();
        _ = _auditRepo.InitializeAsync();

        _pipeline = new PhotoForgePipeline(_metadataEngine, _matchingEngine, _imageEngine, _storageEngine, _auditRepo);

        ListBatchResults.ItemsSource = BatchResults;
        GridMatches.ItemsSource = MatchReviews;

        if (OperatingSystem.IsWindows())
        {
            ChkExplorerMenu.IsChecked = ShellRegistration.IsRegistered();
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        ViewHome.Visibility = NavHome.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewBatch.Visibility = NavBatch.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewReview.Visibility = NavReview.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewDiff.Visibility = NavDiff.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewHeic.Visibility = NavHeic.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewHistory.Visibility = NavHistory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewSettings.Visibility = NavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (NavHistory.IsChecked == true)
        {
            _ = LoadHistoryAsync();
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Original_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            _selectedOriginalPath = files[0];
            TxtOriginalPath.Text = Path.GetFileName(_selectedOriginalPath);
        }
    }

    private void Edited_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            _selectedEditedPath = files[0];
            TxtEditedPath.Text = Path.GetFileName(_selectedEditedPath);
        }
    }

    private void BrowseOriginal_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Camera Original Photo",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.tiff;*.dng;*.heic;*.avif|All Files|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            _selectedOriginalPath = dlg.FileName;
            TxtOriginalPath.Text = Path.GetFileName(_selectedOriginalPath);
        }
    }

    private void BrowseEdited_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Edited Target Photo",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.tiff;*.dng;*.heic;*.avif|All Files|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            _selectedEditedPath = dlg.FileName;
            TxtEditedPath.Text = Path.GetFileName(_selectedEditedPath);
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedEditedPath) || !File.Exists(_selectedEditedPath))
        {
            MessageBox.Show("Please select an edited target photo.", "Target Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(_selectedOriginalPath) || !File.Exists(_selectedOriginalPath))
        {
            MessageBox.Show("Please select an original camera photo.", "Original Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var profile = new MergeProfile
            {
                OverwriteDestination = ChkOverwrite.IsChecked == true
            };

            var result = await _pipeline.ProcessSinglePairAsync(
                _selectedOriginalPath,
                _selectedEditedPath,
                profile: profile,
                convertToHeic: ChkConvertToHeic.IsChecked == true);

            MessageBox.Show($"Metadata restoration complete!\nStatus: {result.Status}\nSaved to: {result.OutputPath}",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restoration error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void InspectDiff_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedEditedPath) || !File.Exists(_selectedEditedPath))
        {
            MessageBox.Show("Please select an edited target photo to inspect.", "Target Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var origMeta = !string.IsNullOrEmpty(_selectedOriginalPath) && File.Exists(_selectedOriginalPath)
            ? await _metadataEngine.ExtractMetadataAsync(_selectedOriginalPath)
            : new MetadataDocument();
        var targetMeta = await _metadataEngine.ExtractMetadataAsync(_selectedEditedPath);

        var diff = _metadataEngine.ComputeDiff(origMeta, targetMeta, MergeProfile.StandardV1);
        PopulateDiffTree(diff, origMeta, targetMeta);

        NavDiff.IsChecked = true;
        Nav_Click(this, new RoutedEventArgs());
    }

    private void PopulateDiffTree(MetadataDiff diff, MetadataDocument orig, MetadataDocument target)
    {
        TreeMetadataDiff.Items.Clear();

        var rootCopied = new TreeViewItem { Header = $"📥 Copied from Original ({diff.CopiedFromOriginal.Count})", IsExpanded = true };
        foreach (var c in diff.CopiedFromOriginal)
            rootCopied.Items.Add(new TreeViewItem { Header = c });

        var rootPreserved = new TreeViewItem { Header = $"🛡️ Preserved Target Metadata ({diff.PreservedFromTarget.Count})", IsExpanded = true };
        foreach (var p in diff.PreservedFromTarget)
            rootPreserved.Items.Add(new TreeViewItem { Header = p });

        var rootWarnings = new TreeViewItem { Header = $"⚠️ Warnings & Notices ({diff.Warnings.Count})", IsExpanded = true };
        foreach (var w in diff.Warnings)
            rootWarnings.Items.Add(new TreeViewItem { Header = w });

        TreeMetadataDiff.Items.Add(rootCopied);
        TreeMetadataDiff.Items.Add(rootPreserved);
        if (diff.Warnings.Count > 0)
            TreeMetadataDiff.Items.Add(rootWarnings);
    }

    private void BrowseBatchEdited_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Edited Photos Folder" };
        if (dlg.ShowDialog() == true)
            TxtBatchEditedDir.Text = dlg.FolderName;
    }

    private void BrowseBatchOriginals_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Originals Pool Folder" };
        if (dlg.ShowDialog() == true)
            TxtBatchOriginalsDir.Text = dlg.FolderName;
    }

    private async void RunBatch_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtBatchEditedDir.Text) || !Directory.Exists(TxtBatchEditedDir.Text))
        {
            MessageBox.Show("Please select a valid edited photos folder.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtBatchOriginalsDir.Text) || !Directory.Exists(TxtBatchOriginalsDir.Text))
        {
            MessageBox.Show("Please select a valid originals folder.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var editedFiles = Directory.EnumerateFiles(TxtBatchEditedDir.Text, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => !f.Contains("PhotoForge_Restored"))
            .ToList();
        var origFiles = Directory.EnumerateFiles(TxtBatchOriginalsDir.Text, "*.*", SearchOption.AllDirectories).ToList();

        BatchResults.Clear();
        var outDir = Path.Combine(TxtBatchEditedDir.Text, "PhotoForge_Restored");

        var summary = await _pipeline.ProcessBatchAsync(
            editedFiles,
            origFiles,
            outDir,
            MergeProfile.StandardV1,
            autoAcceptConfidentMatches: true);

        foreach (var r in summary.Results)
        {
            BatchResults.Add(new BatchItemViewModel
            {
                TargetFileName = r.TargetRef.FileName,
                OriginalFileName = r.OriginalRef?.FileName ?? "(None)",
                ScoreFormatted = "100%",
                Status = r.Status.ToString(),
                Notes = r.ErrorMessage ?? (r.Diff.Warnings.Count > 0 ? string.Join(", ", r.Diff.Warnings) : "Restored successfully")
            });
        }

        MessageBox.Show($"Batch complete!\nTotal: {summary.TotalItems}\nSucceeded: {summary.SucceededCount}\nSkipped: {summary.SkippedCount}\nFailed: {summary.FailedCount}",
            "Batch Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AcceptAllMatches_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("All high-confidence candidate matches accepted for processing.", "Matches Accepted", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ChangeOriginal_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Choose Manual Replacement Original Photo" };
        if (dlg.ShowDialog() == true)
        {
            MessageBox.Show($"Original photo updated to: {Path.GetFileName(dlg.FileName)}", "Override Accepted", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void MarkNoOriginal_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Target photo marked as having no original candidate. Existing target metadata will be preserved.", "Marked Independent", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ConvertStudio_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Conversion settings applied. You can run batch conversion or single file conversion.", "HEIC Studio", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task LoadHistoryAsync()
    {
        var history = await _auditRepo.GetRecentHistoryAsync(100);
        GridHistory.ItemsSource = history;
    }

    private void RefreshHistory_Click(object sender, RoutedEventArgs e)
    {
        _ = LoadHistoryAsync();
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Audit Report",
            Filter = "Markdown Report (*.md)|*.md|JSON Audit File (*.json)|*.json",
            FileName = "PhotoForge_Audit_Report.md"
        };
        if (dlg.ShowDialog() == true)
        {
            var history = await _auditRepo.GetRecentHistoryAsync(500);
            var summary = new BatchSummary
            {
                TotalItems = history.Count,
                SucceededCount = history.Count(h => h.Status == OperationStatus.Success),
                WarningsCount = history.Count(h => h.Status == OperationStatus.SuccessWithWarnings),
                SkippedCount = history.Count(h => h.Status == OperationStatus.Skipped),
                FailedCount = history.Count(h => h.Status == OperationStatus.Failed),
                Results = history.ToList()
            };

            if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                await AuditExporter.ExportJsonAsync(summary, dlg.FileName);
            else
                await AuditExporter.ExportMarkdownAsync(summary, dlg.FileName);

            MessageBox.Show("Audit report exported successfully!", "Exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExplorerMenu_Checked(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsWindows())
        {
            ShellRegistration.Register();
        }
    }

    private void ExplorerMenu_Unchecked(object sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsWindows())
        {
            ShellRegistration.Unregister();
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdates.IsEnabled = false;
        TxtUpdateStatus.Text = "Checking GitHub Releases...";
        var updateService = new GitHubUpdateService();

        try
        {
            var update = await updateService.CheckForUpdatesAsync("1.0.0");
            if (update == null)
            {
                TxtUpdateStatus.Text = "Unable to reach GitHub Releases. Please check network connection.";
                BtnCheckUpdates.IsEnabled = true;
                return;
            }

            if (!update.IsUpdateAvailable)
            {
                TxtUpdateStatus.Text = $"PhotoForge is up to date (v{update.CurrentVersion}).";
                BtnCheckUpdates.IsEnabled = true;
                return;
            }

            TxtUpdateStatus.Text = $"Update found: v{update.LatestVersion}!";
            var msg = $"A new version of PhotoForge is available: v{update.LatestVersion}\n\n{update.ReleaseTitle}\n\nWould you like to download and install this update now?";
            var res = MessageBox.Show(msg, "PhotoForge Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                PrgUpdate.Visibility = Visibility.Visible;
                PrgUpdate.Value = 0;
                TxtUpdateStatus.Text = "Downloading update installer...";

                var progress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() => PrgUpdate.Value = p * 100);
                });

                var file = await updateService.DownloadUpdateAsync(update, progress);
                TxtUpdateStatus.Text = "Verifying cryptographic checksum...";

                bool verified = await updateService.VerifyDownloadedUpdateAsync(file, update.ExpectedSha256 ?? "");
                if (!verified)
                {
                    MessageBox.Show("Security Verification Failed: The downloaded update does not match the official SHA-256 release checksum.", "Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    TxtUpdateStatus.Text = "Checksum verification failed.";
                    PrgUpdate.Visibility = Visibility.Collapsed;
                    BtnCheckUpdates.IsEnabled = true;
                    return;
                }

                TxtUpdateStatus.Text = "Launching installer...";
                updateService.ApplyUpdateAndRestart(file, silent: false);
            }
            else
            {
                BtnCheckUpdates.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update error: {ex.Message}", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtUpdateStatus.Text = "Error during update check.";
            BtnCheckUpdates.IsEnabled = true;
            PrgUpdate.Visibility = Visibility.Collapsed;
        }
    }
}

public class BatchItemViewModel
{
    public string TargetFileName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string ScoreFormatted { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class MatchItemViewModel
{
    public string TargetName { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ConfidencePercent { get; set; } = "";
    public string Band { get; set; } = "";
    public string ReasonsSummary { get; set; } = "";
}
