using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;

namespace PhotoForge.Matching.Signals;

/// <summary>
/// Evaluates filename similarity by stripping editor suffixes and computing token overlap & edit distance.
/// </summary>
public static class FilenameSignal
{
    private static readonly Regex SuffixRegex = new(
        @"[_\-\s]+(edited|edit|copy|final|export|modified|retouched|v\d+|\(\d+\)|\d{1,2})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static double Evaluate(string targetName, string candidateName, out string? reason)
    {
        reason = null;
        var tBase = Path.GetFileNameWithoutExtension(targetName).ToLowerInvariant();
        var cBase = Path.GetFileNameWithoutExtension(candidateName).ToLowerInvariant();

        if (string.Equals(tBase, cBase, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Exact matching base filename";
            return 1.0;
        }

        string tClean = tBase;
        string prev;
        do
        {
            prev = tClean;
            tClean = SuffixRegex.Replace(tClean, "").Trim();
        } while (tClean != prev);

        string cClean = cBase;
        do
        {
            prev = cClean;
            cClean = SuffixRegex.Replace(cClean, "").Trim();
        } while (cClean != prev);

        if (string.Equals(tClean, cClean, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Matching base filename with common edit suffix stripped";
            return 0.95;
        }

        if (tBase.Contains(cBase) || cBase.Contains(tBase) || tClean.Contains(cClean) || cClean.Contains(tClean))
        {
            reason = "Filename containment relationship";
            return 0.85;
        }

        // Levenshtein distance on clean strings
        int maxLen = Math.Max(tClean.Length, cClean.Length);
        if (maxLen == 0) return 0.0;

        int dist = LevenshteinDistance(tClean, cClean);
        double similarity = 1.0 - ((double)dist / maxLen);

        if (similarity > 0.75)
        {
            reason = $"High filename similarity ({(similarity * 100):F0}%)";
            return similarity;
        }

        return Math.Max(0.0, similarity);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}

/// <summary>
/// Evaluates timestamp proximity between original capture dates and file timestamps.
/// </summary>
public static class TimestampSignal
{
    public static double Evaluate(PhotoRef target, PhotoRef candidate, out string? reason)
    {
        reason = null;

        var tCap = target.Metadata?.BestCaptureDate;
        var cCap = candidate.Metadata?.BestCaptureDate;

        if (tCap.HasValue && cCap.HasValue)
        {
            var diff = (tCap.Value - cCap.Value).Duration();
            if (diff.TotalSeconds < 2)
            {
                reason = "Exact same capture timestamp";
                return 1.0;
            }
            if (diff.TotalMinutes < 1)
            {
                reason = $"Capture timestamps within {diff.TotalSeconds:F0}s";
                return 0.95;
            }
            if (diff.TotalMinutes < 10)
            {
                reason = $"Capture timestamps within {diff.TotalMinutes:F0} minutes";
                return 0.85;
            }
            if (diff.TotalHours < 1)
            {
                return 0.60;
            }
            return 0.0;
        }

        // Target capture date missing, fallback to candidate capture date vs target file modified date
        if (cCap.HasValue && target.ModifiedAtUtc.HasValue)
        {
            // Usually edited file is modified shortly after capture
            var diff = (target.ModifiedAtUtc.Value - cCap.Value).TotalDays;
            if (diff >= -1 && diff <= 30) // edited within 30 days after capture
            {
                reason = "Target modification date is chronologically after original capture";
                return 0.65;
            }
        }

        return 0.40; // Neutral score when timestamps unavailable
    }
}

/// <summary>
/// Evaluates dimension aspect ratio and scaling relationships.
/// </summary>
public static class DimensionsSignal
{
    public static double Evaluate(ImageDimensions targetDim, ImageDimensions candidateDim, out string? reason)
    {
        reason = null;
        if (targetDim.IsEmpty || candidateDim.IsEmpty)
            return 0.5;

        if (targetDim.Width == candidateDim.Width && targetDim.Height == candidateDim.Height)
        {
            reason = "Exact identical pixel dimensions";
            return 1.0;
        }

        double arTarget = targetDim.AspectRatio;
        double arCandidate = candidateDim.AspectRatio;

        // Check if rotated (swap dimensions)
        double arRotated = candidateDim.Height == 0 ? 0.0 : (double)candidateDim.Height / candidateDim.Width;

        double arDiffStandard = Math.Abs(arTarget - arCandidate);
        double arDiffRotated = Math.Abs(arTarget - arRotated);

        if (arDiffStandard < 0.02 || arDiffRotated < 0.02)
        {
            reason = "Matching aspect ratio";
            return 0.90;
        }

        return 0.40;
    }
}

/// <summary>
/// Evaluates surviving camera model and lens clues.
/// </summary>
public static class MetadataRemnantsSignal
{
    public static double Evaluate(PhotoRef target, PhotoRef candidate, out string? reason)
    {
        reason = null;
        var tCam = target.Metadata?.Exif.Camera;
        var cCam = candidate.Metadata?.Exif.Camera;

        if (tCam == null || cCam == null)
            return 0.5;

        bool hasTargetMake = !string.IsNullOrWhiteSpace(tCam.Make);
        bool hasTargetModel = !string.IsNullOrWhiteSpace(tCam.Model);

        if (hasTargetModel && !string.IsNullOrWhiteSpace(cCam.Model))
        {
            if (string.Equals(tCam.Model, cCam.Model, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Same camera model ({cCam.Model})";
                return 1.0;
            }
            return 0.0;
        }

        if (hasTargetMake && !string.IsNullOrWhiteSpace(cCam.Make))
        {
            if (string.Equals(tCam.Make, cCam.Make, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Same camera make ({cCam.Make})";
                return 0.85;
            }
            return 0.1;
        }

        return 0.5;
    }
}

/// <summary>
/// Evaluates directory proximity (same folder, sibling folders like Originals/Edited, parent folders).
/// </summary>
public static class DirectorySignal
{
    private static readonly HashSet<string> RelatedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "originals", "original", "raw", "masters", "master", "source", "sources", "unedited", "camera", "dcim", "edited", "processed"
    };

    public static double Evaluate(string targetPath, string candidatePath, out string? reason)
    {
        reason = null;
        var tDir = Path.GetDirectoryName(targetPath);
        var cDir = Path.GetDirectoryName(candidatePath);

        if (string.Equals(tDir, cDir, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Same directory";
            return 1.0;
        }

        var tFolder = Path.GetFileName(tDir);
        var cFolder = Path.GetFileName(cDir);

        if (!string.IsNullOrEmpty(tFolder) && !string.IsNullOrEmpty(cFolder) &&
            RelatedFolderNames.Contains(tFolder) && RelatedFolderNames.Contains(cFolder))
        {
            reason = $"Paired workflow directories ({cFolder} -> {tFolder})";
            return 0.90;
        }

        var tParent = Path.GetDirectoryName(tDir);
        var cParent = Path.GetDirectoryName(cDir);

        if (string.Equals(tParent, cParent, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Sibling directories under same parent folder";
            return 0.75;
        }

        return 0.20;
    }
}
