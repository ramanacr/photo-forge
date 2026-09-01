using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Services;

namespace PhotoForge.Platform;

public class GitHubUpdateService : IUpdateService
{
    private const string RepoOwner = "ramanacr";
    private const string RepoName = "photo-forge";
    private readonly HttpClient _httpClient;

    public GitHubUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PhotoForge-Updater/1.0.0");
        }
    }

    public async Task<ReleaseUpdateInfo?> CheckForUpdatesAsync(string currentVersion = "1.0.0", CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "v1.0.0";
            var cleanLatest = tagName.TrimStart('v', 'V').Split('-')[0];
            var cleanCurrent = currentVersion.TrimStart('v', 'V').Split('-')[0];

            bool isUpdateAvailable = false;
            if (Version.TryParse(cleanLatest, out var latestVer) && Version.TryParse(cleanCurrent, out var currVer))
            {
                isUpdateAvailable = latestVer > currVer;
            }
            else
            {
                isUpdateAvailable = !string.Equals(cleanLatest, cleanCurrent, StringComparison.OrdinalIgnoreCase);
            }

            var title = root.TryGetProperty("name", out var n) ? n.GetString() ?? tagName : tagName;
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var publishedAt = root.TryGetProperty("published_at", out var p) ? p.GetDateTimeOffset() : DateTimeOffset.UtcNow;

            string downloadUrl = "";
            string downloadFileName = "";
            string? checksumsUrl = null;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.GetProperty("name").GetString() ?? "";
                    var browserUrl = asset.GetProperty("browser_download_url").GetString() ?? "";

                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        assetName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = browserUrl;
                        downloadFileName = assetName;
                    }
                    else if (string.IsNullOrEmpty(downloadUrl) && assetName.EndsWith("-Windows-x64.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = browserUrl;
                        downloadFileName = assetName;
                    }
                    else if (assetName.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        checksumsUrl = browserUrl;
                    }
                }
            }

            string? expectedSha256 = null;
            if (!string.IsNullOrEmpty(checksumsUrl) && !string.IsNullOrEmpty(downloadFileName))
            {
                try
                {
                    var checksumsText = await _httpClient.GetStringAsync(checksumsUrl, ct);
                    foreach (var line in checksumsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.Contains(downloadFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                expectedSha256 = parts[0].Trim().ToLowerInvariant();
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            return new ReleaseUpdateInfo(
                CurrentVersion: currentVersion,
                LatestVersion: cleanLatest,
                IsUpdateAvailable: isUpdateAvailable,
                ReleaseTitle: title,
                ReleaseNotes: body,
                DownloadUrl: downloadUrl,
                DownloadFileName: downloadFileName,
                ExpectedSha256: expectedSha256,
                PublishedAt: publishedAt);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> DownloadUpdateAsync(ReleaseUpdateInfo updateInfo, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            throw new InvalidOperationException("Download URL is empty.");

        var updateDir = Path.Combine(Path.GetTempPath(), "PhotoForge_Updates");
        Directory.CreateDirectory(updateDir);

        var destination = Path.Combine(updateDir, updateInfo.DownloadFileName);
        if (File.Exists(destination))
            File.Delete(destination);

        using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, ct);
            totalRead += read;
            if (totalBytes > 0)
            {
                progress?.Report((double)totalRead / totalBytes);
            }
        }

        progress?.Report(1.0);
        return destination;
    }

    public async Task<bool> VerifyDownloadedUpdateAsync(string filePath, string expectedSha256, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return false;
        if (string.IsNullOrWhiteSpace(expectedSha256)) return true;

        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha.ComputeHashAsync(stream, ct);
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        return string.Equals(hashString, expectedSha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }

    public void ApplyUpdateAndRestart(string installerFilePath, bool silent = false)
    {
        if (!File.Exists(installerFilePath))
            throw new FileNotFoundException("Update installer not found.", installerFilePath);

        var psi = new ProcessStartInfo
        {
            FileName = installerFilePath,
            Arguments = silent ? "/passive" : "",
            UseShellExecute = true
        };

        Process.Start(psi);
        Environment.Exit(0);
    }
}
