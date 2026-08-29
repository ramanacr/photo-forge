using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoForge.Core.Services;

public record ReleaseUpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    bool IsUpdateAvailable,
    string ReleaseTitle,
    string ReleaseNotes,
    string DownloadUrl,
    string DownloadFileName,
    string? ExpectedSha256,
    DateTimeOffset PublishedAt);

public interface IUpdateService
{
    Task<ReleaseUpdateInfo?> CheckForUpdatesAsync(string currentVersion = "1.0.0", CancellationToken ct = default);
    Task<string> DownloadUpdateAsync(ReleaseUpdateInfo updateInfo, IProgress<double>? progress = null, CancellationToken ct = default);
    Task<bool> VerifyDownloadedUpdateAsync(string filePath, string expectedSha256, CancellationToken ct = default);
    void ApplyUpdateAndRestart(string installerFilePath, bool silent = false);
}
