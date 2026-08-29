using System;
using System.Collections.Generic;

namespace PhotoForge.Core.Models;

/// <summary>
/// Policy governing GPS metadata preservation or removal.
/// </summary>
public enum GpsPrivacyPolicy
{
    /// <summary>
    /// Keep exact full-precision GPS coordinates from the original photo.
    /// </summary>
    KeepExact = 0,

    /// <summary>
    /// Completely strip all GPS tags and location information from the output.
    /// </summary>
    Remove = 1,

    /// <summary>
    /// Round coordinates to ~1km precision (approx 2 decimal places) to obscure exact location.
    /// </summary>
    Round = 2,

    /// <summary>
    /// Copy exact GPS coordinates and record an explicit privacy warning.
    /// </summary>
    CopyWithWarning = 3
}

/// <summary>
/// Conversion quality preset for HEIC/AVIF/WebP image generation.
/// </summary>
public enum ConversionQuality
{
    LosslessWhereSupported = 0,
    VeryHigh = 1,
    High = 2,
    Balanced = 3,
    Small = 4,
    Custom = 5
}

/// <summary>
/// Structured difference report detailing what metadata fields were copied, preserved, overwritten, skipped, or failed.
/// </summary>
public record MetadataDiff
{
    public List<string> CopiedFromOriginal { get; init; } = new();
    public List<string> PreservedFromTarget { get; init; } = new();
    public List<string> Overwritten { get; init; } = new();
    public List<string> Skipped { get; init; } = new();
    public List<string> Failed { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    public bool HasWarnings => Warnings.Count > 0;
    public bool HasFailures => Failed.Count > 0;
}

/// <summary>
/// Confidence classification for candidate matching.
/// </summary>
public enum ConfidenceBand
{
    /// <summary>
    /// Score >= 0.95: Auto-accept without user prompt.
    /// </summary>
    AutoAccept = 0,

    /// <summary>
    /// Score 0.85 - 0.949: Highly confident recommendation, easily accepted.
    /// </summary>
    Suggested = 1,

    /// <summary>
    /// Score 0.70 - 0.849: Moderate confidence, user review required.
    /// </summary>
    UserReviewRequired = 2,

    /// <summary>
    /// Score < 0.70: No match meeting acceptable threshold.
    /// </summary>
    NoMatch = 3
}

/// <summary>
/// Individual breakdown of signals contributing to the match score.
/// </summary>
public record SignalScores
{
    public double FilenameScore { get; init; }
    public double TimestampScore { get; init; }
    public double DimensionsScore { get; init; }
    public double MetadataRemnantsScore { get; init; }
    public double PerceptualSimilarityScore { get; init; }
    public double DirectoryRelationScore { get; init; }
    public double AggregateScore { get; init; }
}

/// <summary>
/// Represents an original photo candidate evaluated against an edited target.
/// </summary>
public record MatchingCandidate
{
    public required PhotoRef CandidateRef { get; init; }
    public double Score { get; init; }
    public ConfidenceBand Band { get; init; }
    public SignalScores Signals { get; init; } = new();
    public List<string> Reasons { get; init; } = new();

    public static ConfidenceBand DetermineBand(double score) => score switch
    {
        >= 0.95 => ConfidenceBand.AutoAccept,
        >= 0.85 => ConfidenceBand.Suggested,
        >= 0.70 => ConfidenceBand.UserReviewRequired,
        _ => ConfidenceBand.NoMatch
    };
}

/// <summary>
/// Type of user/system decision for an original/target match pair.
/// </summary>
public enum DecisionType
{
    AutoAccepted = 0,
    UserAccepted = 1,
    UserOverridden = 2,
    MarkedNoOriginal = 3,
    Skipped = 4
}

/// <summary>
/// Pairwise decision linking an edited target with its chosen original or marking it solitary.
/// </summary>
public record MatchingDecision
{
    public required PhotoRef TargetRef { get; init; }
    public PhotoRef? SelectedOriginalRef { get; init; }
    public DecisionType Decision { get; init; }
    public double MatchScore { get; init; }
    public List<string> DecisionNotes { get; init; } = new();
}

/// <summary>
/// Merge profile configuring rules and behaviors for metadata resolution.
/// </summary>
public record MergeProfile
{
    public string Name { get; init; } = "standard-v1";
    public GpsPrivacyPolicy GpsPolicy { get; init; } = GpsPrivacyPolicy.KeepExact;
    public bool PreferTargetForEditState { get; init; } = true;
    public bool PreserveTargetKeywords { get; init; } = true;
    public bool CopyMakerNotesIfSafe { get; init; } = true;
    public bool InvalidateThumbnailIfRotated { get; init; } = true;
    public bool OverwriteDestination { get; init; } = false;
    public ConversionQuality QualityPreset { get; init; } = ConversionQuality.High;
    public int CustomQualityNumber { get; init; } = 85;

    public static MergeProfile StandardV1 => new() { Name = "standard-v1" };
    public static MergeProfile PrivacyStripGps => new() { Name = "privacy-strip-gps", GpsPolicy = GpsPrivacyPolicy.Remove };
    public static MergeProfile PreserveAll => new() { Name = "preserve-all", GpsPolicy = GpsPrivacyPolicy.KeepExact, CopyMakerNotesIfSafe = true };
}

/// <summary>
/// Lifecycle status of a photo migration operation.
/// </summary>
public enum OperationStatus
{
    Success = 0,
    SuccessWithWarnings = 1,
    Skipped = 2,
    NoMatch = 3,
    UserReviewRequired = 4,
    Unsupported = 5,
    Failed = 6,
    Cancelled = 7
}

/// <summary>
/// Result of verifying the written destination file independently.
/// </summary>
public record VerificationResult
{
    public bool IsValid { get; init; }
    public bool CanBeReopened { get; init; }
    public bool HasValidDimensions { get; init; }
    public bool HasRequiredMetadata { get; init; }
    public bool HasMigrationMarker { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> VerifiedFields { get; init; } = new();
}

/// <summary>
/// Comprehensive result of a single photo metadata continuity operation.
/// </summary>
public record OperationResult
{
    public required string OperationId { get; init; }
    public required PhotoRef TargetRef { get; init; }
    public PhotoRef? OriginalRef { get; init; }
    public string? OutputPath { get; init; }
    public OperationStatus Status { get; init; }
    public MetadataDiff Diff { get; init; } = new();
    public VerificationResult? Verification { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;

    public static OperationResult CreateSuccess(
        PhotoRef target,
        PhotoRef? original,
        string outputPath,
        MetadataDiff diff,
        VerificationResult verification,
        TimeSpan duration) => new()
    {
        OperationId = Guid.NewGuid().ToString("N"),
        TargetRef = target,
        OriginalRef = original,
        OutputPath = outputPath,
        Status = diff.HasWarnings ? OperationStatus.SuccessWithWarnings : OperationStatus.Success,
        Diff = diff,
        Verification = verification,
        Duration = duration
    };

    public static OperationResult CreateSkipped(PhotoRef target, string reason) => new()
    {
        OperationId = Guid.NewGuid().ToString("N"),
        TargetRef = target,
        Status = OperationStatus.Skipped,
        ErrorMessage = reason,
        Duration = TimeSpan.Zero
    };

    public static OperationResult CreateFailed(PhotoRef target, string error, TimeSpan duration) => new()
    {
        OperationId = Guid.NewGuid().ToString("N"),
        TargetRef = target,
        Status = OperationStatus.Failed,
        ErrorMessage = error,
        Duration = duration
    };
}

/// <summary>
/// Summary report for a batch processing execution.
/// </summary>
public record BatchSummary
{
    public string BatchId { get; set; } = Guid.NewGuid().ToString("N");
    public int TotalItems { get; set; }
    public int SucceededCount { get; set; }
    public int WarningsCount { get; set; }
    public int SkippedCount { get; set; }
    public int NoMatchCount { get; set; }
    public int ReviewRequiredCount { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<OperationResult> Results { get; set; } = new();
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime FinishedAtUtc { get; set; } = DateTime.UtcNow;
}
