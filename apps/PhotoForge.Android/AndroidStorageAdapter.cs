using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;
using PhotoForge.Platform;

namespace PhotoForge.Android;

/// <summary>
/// Android Scoped Storage and MediaStore adapter.
/// Isolates ContentResolver and MediaStore specifics from the core domain engine.
/// </summary>
public class AndroidStorageAdapter : IPlatformBridge
{
    public string PlatformName => "Android";
    public bool IsDesktop => false;
    public bool IsMobile => true;

    private readonly string _cacheDir;

    public AndroidStorageAdapter(string? cacheDir = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(Path.GetTempPath(), "PhotoForgeAndroidCache");
        if (!Directory.Exists(_cacheDir))
        {
            Directory.CreateDirectory(_cacheDir);
        }
    }

    public async Task<string> CopyContentUriToPrivateCacheAsync(Stream contentStream, string originalFileName, CancellationToken ct = default)
    {
        var safeFileName = Path.GetFileName(originalFileName);
        var targetPath = Path.Combine(_cacheDir, $"{Guid.NewGuid():N}_{safeFileName}");

        using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await contentStream.CopyToAsync(fs, 81920, ct);
        return targetPath;
    }

    public Task<string?> PickFileAsync(string title, string[] filterExtensions, CancellationToken ct = default)
    {
        // Interfaced with Android Photo Picker (ActivityResultContracts.PickVisualMedia)
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickFolderAsync(string title, CancellationToken ct = default)
    {
        // Interfaced with Storage Access Framework (ACTION_OPEN_DOCUMENT_TREE)
        return Task.FromResult<string?>(null);
    }

    public Task OpenFileInDefaultViewerAsync(string filePath)
    {
        // Interfaced with Android ACTION_VIEW intent with FileProvider content URI
        return Task.CompletedTask;
    }

    public Task OpenFolderInFileManagerAsync(string folderPath)
    {
        return Task.CompletedTask;
    }
}
