using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;

namespace PhotoForge.Storage;

/// <summary>
/// Safe storage engine guaranteeing source immutability, temp file lifecycle, and atomic commits.
/// </summary>
public class StorageEngine : IStorageEngine
{
    public async Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hashBytes = await Task.Run(() => sha.ComputeHash(stream), ct);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public string CreateTempFilePath(string targetPath)
    {
        var dir = Path.GetDirectoryName(targetPath) ?? Path.GetTempPath();
        var fileName = Path.GetFileName(targetPath);
        var unique = Guid.NewGuid().ToString("N").Substring(0, 12);
        return Path.Combine(dir, $"{fileName}.tmp.photoforge.{unique}");
    }

    public async Task<bool> VerifySourceImmutabilityAsync(string sourcePath, string expectedSha256, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            return false;

        var currentSha = await ComputeFileSha256Async(sourcePath, ct);
        return string.Equals(currentSha, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    public async Task AtomicCommitAsync(string tempFilePath, string destinationPath, bool overwrite = false, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            if (!File.Exists(tempFilePath))
                throw new PhotoForgeException(ErrorCategory.AtomicCommitFailure, $"Temporary output file does not exist: {tempFilePath}");

            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            if (File.Exists(destinationPath))
            {
                if (!overwrite)
                    throw new PhotoForgeException(ErrorCategory.OutputConflict, $"Destination file already exists: {destinationPath}");

                // Atomic replace
                File.Move(tempFilePath, destinationPath, overwrite: true);
            }
            else
            {
                File.Move(tempFilePath, destinationPath);
            }
        }, ct);
    }

    public void SafeDeleteTemp(string tempFilePath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
