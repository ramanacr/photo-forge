using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;

namespace PhotoForge.Core.Pipeline;

/// <summary>
/// Top-level pipeline orchestrator executing photo metadata continuity and format conversion.
/// </summary>
public class PhotoForgePipeline : IPhotoForgePipeline
{
    private readonly IMetadataEngine _metadataEngine;
    private readonly IMatchingEngine _matchingEngine;
    private readonly IImageEngine _imageEngine;
    private readonly IStorageEngine _storageEngine;
    private readonly IAuditRepository _auditRepo;

    public PhotoForgePipeline(
        IMetadataEngine metadataEngine,
        IMatchingEngine matchingEngine,
        IImageEngine imageEngine,
        IStorageEngine storageEngine,
        IAuditRepository auditRepo)
    {
        _metadataEngine = metadataEngine;
        _matchingEngine = matchingEngine;
        _imageEngine = imageEngine;
        _storageEngine = storageEngine;
        _auditRepo = auditRepo;
    }

    public async Task<OperationResult> ProcessSinglePairAsync(
        string originalPath,
        string editedPath,
        string? outputPath = null,
        MergeProfile? profile = null,
        bool convertToHeic = false,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        profile ??= MergeProfile.StandardV1;

        progress?.Report(0.05);

        if (!File.Exists(originalPath))
            throw new PhotoForgeException(ErrorCategory.InvalidInput, $"Original file does not exist: {originalPath}");
        if (!File.Exists(editedPath))
            throw new PhotoForgeException(ErrorCategory.InvalidInput, $"Edited file does not exist: {editedPath}");

        if (string.Equals(Path.GetFullPath(originalPath), Path.GetFullPath(editedPath), StringComparison.OrdinalIgnoreCase))
            throw new PhotoForgeException(ErrorCategory.InvalidInput, "Original and edited photos point to the exact same file path.");

        // 1. Compute fingerprints & format sniffing
        progress?.Report(0.15);
        var origSha = await _storageEngine.ComputeFileSha256Async(originalPath, ct);
        var origFormat = _imageEngine.SniffFormat(originalPath);
        var origDim = await _imageEngine.InspectDimensionsAsync(originalPath, ct);
        var origMeta = await _metadataEngine.ExtractMetadataAsync(originalPath, ct);

        var origRef = PhotoRef.Create(originalPath, origFormat, new FileInfo(originalPath).Length, origSha, origDim, metadata: origMeta);

        var targetSha = await _storageEngine.ComputeFileSha256Async(editedPath, ct);
        var targetFormat = _imageEngine.SniffFormat(editedPath);
        var targetDim = await _imageEngine.InspectDimensionsAsync(editedPath, ct);
        var targetMeta = await _metadataEngine.ExtractMetadataAsync(editedPath, ct);

        var targetRef = PhotoRef.Create(editedPath, targetFormat, new FileInfo(editedPath).Length, targetSha, targetDim, metadata: targetMeta);

        // 2. Check Idempotency (Marker check)
        progress?.Report(0.25);
        if (targetMeta.Marker != null &&
            string.Equals(targetMeta.Marker.SourceFingerprint, origSha, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(targetMeta.Marker.Profile, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            var skipResult = OperationResult.CreateSkipped(targetRef, "Photo is already migrated with identical source and profile.");
            await _auditRepo.RecordMigrationAsync(skipResult, profile.Name, ct);
            progress?.Report(1.0);
            return skipResult;
        }

        // 3. Resolve destination path
        string finalDestination = outputPath ?? (convertToHeic
            ? Path.Combine(Path.GetDirectoryName(editedPath)!, $"{Path.GetFileNameWithoutExtension(editedPath)}_restored.heic")
            : (profile.OverwriteDestination
                ? editedPath
                : Path.Combine(Path.GetDirectoryName(editedPath)!, $"{Path.GetFileNameWithoutExtension(editedPath)}_restored{Path.GetExtension(editedPath)}")));

        string tempOutPath = _storageEngine.CreateTempFilePath(finalDestination);

        try
        {
            // 4. Merge Metadata
            progress?.Report(0.40);
            var mergedMeta = _metadataEngine.MergeMetadata(origMeta, targetMeta, origSha, profile, out var diff);

            // 5. Image Transformation / Re-encoding
            progress?.Report(0.60);
            await _imageEngine.ConvertToHeicAsync(editedPath, tempOutPath, mergedMeta, profile.QualityPreset, ct);

            // 6. Independent Verification
            progress?.Report(0.80);
            var verification = await VerifyOutputAsync(tempOutPath, ct);
            if (!verification.IsValid)
            {
                throw new PhotoForgeException(ErrorCategory.VerificationFailure,
                    $"Independent verification failed on written output: {string.Join("; ", verification.Errors)}");
            }

            // 7. Source Immutability Check (Invariant INV-01)
            progress?.Report(0.90);
            bool sourceUntouched = await _storageEngine.VerifySourceImmutabilityAsync(originalPath, origSha, ct);
            if (!sourceUntouched)
            {
                throw new PhotoForgeException(ErrorCategory.InternalError, "CRITICAL: Source original was mutated during operation!");
            }

            // 8. Atomic Commit
            await _storageEngine.AtomicCommitAsync(tempOutPath, finalDestination, overwrite: true, ct);

            sw.Stop();
            var result = OperationResult.CreateSuccess(targetRef, origRef, finalDestination, diff, verification, sw.Elapsed);
            await _auditRepo.RecordMigrationAsync(result, profile.Name, ct);

            progress?.Report(1.0);
            return result;
        }
        catch (Exception ex)
        {
            _storageEngine.SafeDeleteTemp(tempOutPath);
            sw.Stop();

            var failResult = OperationResult.CreateFailed(targetRef, ex.Message, sw.Elapsed);
            await _auditRepo.RecordMigrationAsync(failResult, profile.Name, ct);
            throw;
        }
    }

    public async Task<BatchSummary> ProcessBatchAsync(
        IReadOnlyList<string> editedPaths,
        IReadOnlyList<string> originalCandidatePaths,
        string outputDirectory,
        MergeProfile? profile = null,
        bool convertToHeic = false,
        bool autoAcceptConfidentMatches = true,
        IProgress<(int current, int total, string currentFile)>? progress = null,
        CancellationToken ct = default)
    {
        profile ??= MergeProfile.StandardV1;
        var batchSw = Stopwatch.StartNew();
        var summary = new BatchSummary
        {
            TotalItems = editedPaths.Count,
            StartedAtUtc = DateTime.UtcNow
        };

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // 1. Index original candidates
        var originalRefs = new List<PhotoRef>();
        foreach (var origPath in originalCandidatePaths)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(origPath)) continue;

            var sha = await _storageEngine.ComputeFileSha256Async(origPath, ct);
            var fmt = _imageEngine.SniffFormat(origPath);
            var dim = await _imageEngine.InspectDimensionsAsync(origPath, ct);
            var meta = await _metadataEngine.ExtractMetadataAsync(origPath, ct);
            var phash = await _imageEngine.ComputePerceptualHashAsync(origPath, ct);

            originalRefs.Add(PhotoRef.Create(origPath, fmt, new FileInfo(origPath).Length, sha, dim, metadata: meta, perceptualHash: phash));
        }

        int index = 0;
        foreach (var editedPath in editedPaths)
        {
            ct.ThrowIfCancellationRequested();
            index++;
            progress?.Report((index, editedPaths.Count, Path.GetFileName(editedPath)));

            if (!File.Exists(editedPath))
            {
                summary.FailedCount++;
                continue;
            }

            var tSha = await _storageEngine.ComputeFileSha256Async(editedPath, ct);
            var tFmt = _imageEngine.SniffFormat(editedPath);
            var tDim = await _imageEngine.InspectDimensionsAsync(editedPath, ct);
            var tMeta = await _metadataEngine.ExtractMetadataAsync(editedPath, ct);
            var tPhash = await _imageEngine.ComputePerceptualHashAsync(editedPath, ct);

            var targetRef = PhotoRef.Create(editedPath, tFmt, new FileInfo(editedPath).Length, tSha, tDim, metadata: tMeta, perceptualHash: tPhash);

            // Find matching original
            var bestMatch = await _matchingEngine.FindBestMatchAsync(targetRef, originalRefs, ct);

            if (bestMatch == null || bestMatch.Band == ConfidenceBand.NoMatch)
            {
                summary.NoMatchCount++;
                summary.Results.Add(new OperationResult
                {
                    OperationId = Guid.NewGuid().ToString("N"),
                    TargetRef = targetRef,
                    Status = OperationStatus.NoMatch,
                    ErrorMessage = "No suitable original found in candidate pool",
                    Duration = TimeSpan.Zero
                });
                continue;
            }

            if (bestMatch.Band == ConfidenceBand.UserReviewRequired && !autoAcceptConfidentMatches)
            {
                summary.ReviewRequiredCount++;
                summary.Results.Add(new OperationResult
                {
                    OperationId = Guid.NewGuid().ToString("N"),
                    TargetRef = targetRef,
                    OriginalRef = bestMatch.CandidateRef,
                    Status = OperationStatus.UserReviewRequired,
                    ErrorMessage = $"Match score ({bestMatch.Score:P1}) requires user review",
                    Duration = TimeSpan.Zero
                });
                continue;
            }

            // Determine output path in batch output directory
            string outFileName = convertToHeic
                ? $"{Path.GetFileNameWithoutExtension(editedPath)}.heic"
                : Path.GetFileName(editedPath);
            string itemOutPath = Path.Combine(outputDirectory, outFileName);

            try
            {
                var opResult = await ProcessSinglePairAsync(
                    bestMatch.CandidateRef.FilePath,
                    editedPath,
                    itemOutPath,
                    profile,
                    convertToHeic,
                    null,
                    ct);

                summary.Results.Add(opResult);

                if (opResult.Status == OperationStatus.Success)
                    summary.SucceededCount++;
                else if (opResult.Status == OperationStatus.SuccessWithWarnings)
                    summary.WarningsCount++;
                else if (opResult.Status == OperationStatus.Skipped)
                    summary.SkippedCount++;
                else
                    summary.FailedCount++;
            }
            catch (Exception ex)
            {
                summary.FailedCount++;
                summary.Results.Add(OperationResult.CreateFailed(targetRef, ex.Message, TimeSpan.Zero));
            }
        }

        batchSw.Stop();
        summary.TotalDuration = batchSw.Elapsed;
        summary.FinishedAtUtc = DateTime.UtcNow;

        await _auditRepo.RecordBatchAsync(summary, ct);
        return summary;
    }

    public async Task<VerificationResult> VerifyOutputAsync(string outputPath, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var errors = new List<string>();
            var verifiedFields = new List<string>();

            if (!File.Exists(outputPath))
            {
                errors.Add("Destination file does not exist on disk.");
                return new VerificationResult { IsValid = false, Errors = errors };
            }

            var fi = new FileInfo(outputPath);
            if (fi.Length < 100)
            {
                errors.Add($"Output file is suspiciously small ({fi.Length} bytes).");
            }

            // Re-read dimensions
            var dims = await _imageEngine.InspectDimensionsAsync(outputPath, ct);
            bool hasValidDims = !dims.IsEmpty;
            if (hasValidDims)
            {
                verifiedFields.Add($"Dimensions: {dims.Width}x{dims.Height}");
            }
            else
            {
                errors.Add("Could not read valid image dimensions from output.");
            }

            // Re-read metadata
            var meta = await _metadataEngine.ExtractMetadataAsync(outputPath, ct);
            bool hasMarker = meta.Marker != null && meta.Marker.Processed;
            if (hasMarker)
            {
                verifiedFields.Add("PhotoForge.MigrationMarker");
            }

            if (meta.Exif.DateTimeOriginal.HasValue)
                verifiedFields.Add($"DateTimeOriginal: {meta.Exif.DateTimeOriginal:yyyy-MM-dd HH:mm:ss}");

            if (meta.Gps != null)
                verifiedFields.Add($"GPS: {meta.Gps.Latitude:F4}, {meta.Gps.Longitude:F4}");

            if (!string.IsNullOrEmpty(meta.Exif.Camera.Model))
                verifiedFields.Add($"Camera: {meta.Exif.Camera.Model}");

            return new VerificationResult
            {
                IsValid = errors.Count == 0 && hasValidDims,
                CanBeReopened = hasValidDims,
                HasValidDimensions = hasValidDims,
                HasRequiredMetadata = meta.HasCaptureDate || meta.HasGps,
                HasMigrationMarker = hasMarker,
                Errors = errors,
                VerifiedFields = verifiedFields
            };
        }, ct);
    }
}
