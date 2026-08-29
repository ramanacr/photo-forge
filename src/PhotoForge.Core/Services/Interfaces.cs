using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;

namespace PhotoForge.Core.Services;

/// <summary>
/// Service contract for image metadata parsing, normalization, merging, and diff computation.
/// </summary>
public interface IMetadataEngine
{
    Task<MetadataDocument> ExtractMetadataAsync(string filePath, CancellationToken ct = default);
    Task<MetadataDocument> ExtractMetadataAsync(Stream stream, PhotoFormat format, CancellationToken ct = default);
    MetadataDiff ComputeDiff(MetadataDocument original, MetadataDocument target, MergeProfile profile);
    MetadataDocument MergeMetadata(MetadataDocument original, MetadataDocument target, string sourceFingerprint, MergeProfile profile, out MetadataDiff diff);
    Task InjectMetadataAsync(Stream inputImage, Stream outputImage, MetadataDocument mergedMetadata, PhotoFormat format, CancellationToken ct = default);
}

/// <summary>
/// Service contract for identifying and ranking original photo candidates for an edited target.
/// </summary>
public interface IMatchingEngine
{
    Task<IReadOnlyList<MatchingCandidate>> FindCandidatesAsync(
        PhotoRef target,
        IEnumerable<PhotoRef> candidatePool,
        CancellationToken ct = default);

    Task<MatchingCandidate?> FindBestMatchAsync(
        PhotoRef target,
        IEnumerable<PhotoRef> candidatePool,
        CancellationToken ct = default);
}

/// <summary>
/// Service contract for image decoding, encoding, format detection, perceptual hashing, and HEIC conversion.
/// </summary>
public interface IImageEngine
{
    PhotoFormat SniffFormat(Stream stream);
    PhotoFormat SniffFormat(string filePath);
    Task<ImageDimensions> InspectDimensionsAsync(string filePath, CancellationToken ct = default);
    Task<ulong> ComputePerceptualHashAsync(string filePath, CancellationToken ct = default);
    Task<ulong> ComputePerceptualHashAsync(Stream stream, PhotoFormat format, CancellationToken ct = default);
    double ComparePerceptualHashes(ulong hash1, ulong hash2);
    Task<byte[]> GenerateThumbnailAsync(string filePath, int maxWidth = 256, int maxHeight = 256, CancellationToken ct = default);
    Task ConvertToHeicAsync(
        string sourcePath,
        string destinationPath,
        MetadataDocument? metadataToInject = null,
        ConversionQuality quality = ConversionQuality.High,
        CancellationToken ct = default);
}

/// <summary>
/// Service contract for safe filesystem operations, atomic commits, temp file lifecycle, and read-only source immutability.
/// </summary>
public interface IStorageEngine
{
    Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct = default);
    string CreateTempFilePath(string targetPath);
    Task<bool> VerifySourceImmutabilityAsync(string sourcePath, string expectedSha256, CancellationToken ct = default);
    Task AtomicCommitAsync(string tempFilePath, string destinationPath, bool overwrite = false, CancellationToken ct = default);
    void SafeDeleteTemp(string tempFilePath);
}

/// <summary>
/// Local SQLite operational repository for migration history, audit logging, and candidate caching.
/// </summary>
public interface IAuditRepository
{
    Task InitializeAsync(CancellationToken ct = default);
    Task RecordMigrationAsync(OperationResult result, string profileName, CancellationToken ct = default);
    Task<MigrationMarker?> GetMigrationRecordAsync(string targetFingerprint, CancellationToken ct = default);
    Task RecordBatchAsync(BatchSummary summary, CancellationToken ct = default);
    Task<IReadOnlyList<OperationResult>> GetRecentHistoryAsync(int limit = 100, CancellationToken ct = default);
    Task CacheCandidateFingerprintAsync(string filePath, string sha256, ulong perceptualHash, CancellationToken ct = default);
}

/// <summary>
/// Top-level pipeline orchestrator executing photo restoration and format conversion workflows.
/// </summary>
public interface IPhotoForgePipeline
{
    Task<OperationResult> ProcessSinglePairAsync(
        string originalPath,
        string editedPath,
        string? outputPath = null,
        MergeProfile? profile = null,
        bool convertToHeic = false,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    Task<BatchSummary> ProcessBatchAsync(
        IReadOnlyList<string> editedPaths,
        IReadOnlyList<string> originalCandidatePaths,
        string outputDirectory,
        MergeProfile? profile = null,
        bool convertToHeic = false,
        bool autoAcceptConfidentMatches = true,
        IProgress<(int current, int total, string currentFile)>? progress = null,
        CancellationToken ct = default);

    Task<VerificationResult> VerifyOutputAsync(string outputPath, CancellationToken ct = default);
}
