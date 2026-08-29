using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;
using PhotoForge.Matching.Signals;

namespace PhotoForge.Matching;

/// <summary>
/// Deterministic multi-signal matching engine identifying original photos for edited targets.
/// </summary>
public class MatchingEngine : IMatchingEngine
{
    private readonly IImageEngine _imageEngine;

    // Default starting weights calibrated for common editor workflows
    public double FilenameWeight { get; set; } = 0.20;
    public double TimestampWeight { get; set; } = 0.15;
    public double DimensionsWeight { get; set; } = 0.10;
    public double MetadataRemnantsWeight { get; set; } = 0.10;
    public double PerceptualWeight { get; set; } = 0.35;
    public double DirectoryWeight { get; set; } = 0.10;

    public MatchingEngine(IImageEngine imageEngine)
    {
        _imageEngine = imageEngine;
    }

    public async Task<IReadOnlyList<MatchingCandidate>> FindCandidatesAsync(
        PhotoRef target,
        IEnumerable<PhotoRef> candidatePool,
        CancellationToken ct = default)
    {
        var poolList = candidatePool
            .Where(c => !string.Equals(c.FilePath, target.FilePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (poolList.Count == 0)
            return Array.Empty<MatchingCandidate>();

        // 1. Fast pre-filter staged evaluation
        var evaluated = new List<MatchingCandidate>();

        // Ensure target perceptual hash is available if possible
        ulong targetPhash = target.PerceptualHash ?? 0;
        if (targetPhash == 0 && System.IO.File.Exists(target.FilePath))
        {
            targetPhash = await _imageEngine.ComputePerceptualHashAsync(target.FilePath, ct);
        }

        foreach (var candidate in poolList)
        {
            ct.ThrowIfCancellationRequested();

            var reasons = new List<string>();

            // Signal 1: Filename
            double sFilename = FilenameSignal.Evaluate(target.FileName, candidate.FileName, out var rFilename);
            if (rFilename != null) reasons.Add(rFilename);

            // Signal 2: Timestamp
            double sTimestamp = TimestampSignal.Evaluate(target, candidate, out var rTimestamp);
            if (rTimestamp != null) reasons.Add(rTimestamp);

            // Signal 3: Dimensions
            double sDimensions = DimensionsSignal.Evaluate(target.Dimensions, candidate.Dimensions, out var rDimensions);
            if (rDimensions != null) reasons.Add(rDimensions);

            // Signal 4: Metadata Remnants
            double sMetadata = MetadataRemnantsSignal.Evaluate(target, candidate, out var rMetadata);
            if (rMetadata != null) reasons.Add(rMetadata);

            // Signal 5: Directory Proximity
            double sDirectory = DirectorySignal.Evaluate(target.FilePath, candidate.FilePath, out var rDirectory);
            if (rDirectory != null) reasons.Add(rDirectory);

            // Signal 6: Perceptual Image Similarity
            double sPerceptual = 0.0;
            ulong candidatePhash = candidate.PerceptualHash ?? 0;
            if (candidatePhash == 0 && System.IO.File.Exists(candidate.FilePath))
            {
                // Only compute expensive perceptual hash if candidate has promising preliminary signals (>0.30)
                double preScore = (sFilename + sTimestamp + sDimensions + sMetadata + sDirectory) / 5.0;
                if (preScore >= 0.30 || poolList.Count <= 20)
                {
                    candidatePhash = await _imageEngine.ComputePerceptualHashAsync(candidate.FilePath, ct);
                }
            }

            if (targetPhash != 0 && candidatePhash != 0)
            {
                sPerceptual = _imageEngine.ComparePerceptualHashes(targetPhash, candidatePhash);
                if (sPerceptual >= 0.85)
                {
                    reasons.Add($"High visual perceptual similarity ({(sPerceptual * 100):F0}%)");
                }
                else if (sPerceptual >= 0.70)
                {
                    reasons.Add($"Moderate visual similarity ({(sPerceptual * 100):F0}%)");
                }
            }

            // Calculate aggregate score
            double aggregateScore;
            if (sPerceptual >= 0.70 && sFilename >= 0.80)
            {
                aggregateScore = 0.85 + (0.10 * sPerceptual) + (0.05 * sDirectory);
            }
            else if (sFilename >= 0.90 && sDirectory >= 0.90)
            {
                aggregateScore = 0.85 + (0.10 * sDimensions) + (0.05 * sPerceptual);
            }
            else if (targetPhash != 0 && candidatePhash != 0)
            {
                aggregateScore = (FilenameWeight * sFilename) +
                                 (TimestampWeight * sTimestamp) +
                                 (DimensionsWeight * sDimensions) +
                                 (MetadataRemnantsWeight * sMetadata) +
                                 (PerceptualWeight * sPerceptual) +
                                 (DirectoryWeight * sDirectory);
            }
            else
            {
                // Rebalance weights when perceptual hash is absent
                aggregateScore = (0.30 * sFilename) +
                                 (0.25 * sTimestamp) +
                                 (0.15 * sDimensions) +
                                 (0.15 * sMetadata) +
                                 (0.15 * sDirectory);
            }

            aggregateScore = Math.Min(1.0, Math.Max(0.0, aggregateScore));

            var signalBreakdown = new SignalScores
            {
                FilenameScore = sFilename,
                TimestampScore = sTimestamp,
                DimensionsScore = sDimensions,
                MetadataRemnantsScore = sMetadata,
                PerceptualSimilarityScore = sPerceptual,
                DirectoryRelationScore = sDirectory,
                AggregateScore = aggregateScore
            };

            var band = MatchingCandidate.DetermineBand(aggregateScore);

            evaluated.Add(new MatchingCandidate
            {
                CandidateRef = candidate with { PerceptualHash = candidatePhash },
                Score = aggregateScore,
                Band = band,
                Signals = signalBreakdown,
                Reasons = reasons
            });
        }

        return evaluated
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    public async Task<MatchingCandidate?> FindBestMatchAsync(
        PhotoRef target,
        IEnumerable<PhotoRef> candidatePool,
        CancellationToken ct = default)
    {
        var candidates = await FindCandidatesAsync(target, candidatePool, ct);
        return candidates.FirstOrDefault();
    }
}
