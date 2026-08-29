using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;

namespace PhotoForge.Platform;

/// <summary>
/// Platform-specific operations interface.
/// </summary>
public interface IPlatformBridge
{
    string PlatformName { get; }
    bool IsDesktop { get; }
    bool IsMobile { get; }
    Task<string?> PickFileAsync(string title, string[] filterExtensions, CancellationToken ct = default);
    Task<string?> PickFolderAsync(string title, CancellationToken ct = default);
    Task OpenFileInDefaultViewerAsync(string filePath);
    Task OpenFolderInFileManagerAsync(string folderPath);
}

/// <summary>
/// Default Windows platform implementation.
/// </summary>
public class WindowsPlatformBridge : IPlatformBridge
{
    public string PlatformName => "Windows";
    public bool IsDesktop => true;
    public bool IsMobile => false;

    public Task<string?> PickFileAsync(string title, string[] filterExtensions, CancellationToken ct = default)
    {
        // Handled via Desktop GUI dialogs or CLI prompt
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickFolderAsync(string title, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task OpenFileInDefaultViewerAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        return Task.CompletedTask;
    }

    public Task OpenFolderInFileManagerAsync(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        return Task.CompletedTask;
    }
}
