using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;

namespace PhotoForge.Audit;

/// <summary>
/// Structured exporter for operation audits in JSON, Markdown, and CSV formats.
/// </summary>
public static class AuditExporter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task ExportJsonAsync(BatchSummary summary, string filePath, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(summary, JsonOpts);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, ct);
    }

    public static async Task ExportJsonAsync(OperationResult result, string filePath, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(result, JsonOpts);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, ct);
    }

    public static string ToJson(BatchSummary summary) => JsonSerializer.Serialize(summary, JsonOpts);
    public static string ToJson(OperationResult result) => JsonSerializer.Serialize(result, JsonOpts);

    public static async Task ExportMarkdownAsync(BatchSummary summary, string filePath, CancellationToken ct = default)
    {
        var md = GenerateMarkdownSummary(summary);
        await File.WriteAllTextAsync(filePath, md, Encoding.UTF8, ct);
    }

    public static string GenerateMarkdownSummary(BatchSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# PhotoForge Batch Migration Report");
        sb.AppendLine();
        sb.AppendLine($"- **Batch ID:** `{summary.BatchId}`");
        sb.AppendLine($"- **Executed At:** `{summary.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC`");
        sb.AppendLine($"- **Duration:** `{summary.TotalDuration.TotalSeconds:F2}s`");
        sb.AppendLine($"- **Total Items:** `{summary.TotalItems}`");
        sb.AppendLine($"- **Succeeded:** `{summary.SucceededCount}`");
        sb.AppendLine($"- **With Warnings:** `{summary.WarningsCount}`");
        sb.AppendLine($"- **Skipped (Already Migrated):** `{summary.SkippedCount}`");
        sb.AppendLine($"- **No Match Found:** `{summary.NoMatchCount}`");
        sb.AppendLine($"- **Failed:** `{summary.FailedCount}`");
        sb.AppendLine();
        sb.AppendLine("## Detailed Items");
        sb.AppendLine();
        sb.AppendLine("| Target File | Original File | Status | Duration | Warnings / Errors |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (var r in summary.Results)
        {
            var targetName = Path.GetFileName(r.TargetRef.FilePath);
            var origName = r.OriginalRef != null ? Path.GetFileName(r.OriginalRef.FilePath) : "-";
            var statusStr = r.Status.ToString();
            var durStr = $"{r.Duration.TotalMilliseconds:F0}ms";
            var notes = r.ErrorMessage ?? (r.Diff.Warnings.Count > 0 ? string.Join("; ", r.Diff.Warnings) : "-");

            sb.AppendLine($"| `{targetName}` | `{origName}` | **{statusStr}** | {durStr} | {notes} |");
        }

        return sb.ToString();
    }
}
