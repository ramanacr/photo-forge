using System;

namespace PhotoForge.Core.Models;

/// <summary>
/// Categories of typed errors in PhotoForge.
/// </summary>
public enum ErrorCategory
{
    InvalidInput = 1,
    UnsupportedFormat = 2,
    MetadataParseFailure = 3,
    MetadataWriteFailure = 4,
    NoMatch = 5,
    LowConfidenceMatch = 6,
    OutputConflict = 7,
    AtomicCommitFailure = 8,
    VerificationFailure = 9,
    Cancelled = 10,
    InternalError = 11
}

/// <summary>
/// Typed domain error for PhotoForge operations.
/// </summary>
public record TypedError
{
    public ErrorCategory Category { get; init; }
    public required string UserMessage { get; init; }
    public string? DiagnosticDetails { get; init; }
    public string? TargetPath { get; init; }
    public string? SourcePath { get; init; }

    public static TypedError Create(ErrorCategory category, string userMessage, string? diagnostic = null, string? targetPath = null, string? sourcePath = null) =>
        new()
        {
            Category = category,
            UserMessage = userMessage,
            DiagnosticDetails = diagnostic,
            TargetPath = targetPath,
            SourcePath = sourcePath
        };
}

/// <summary>
/// Domain exception carrying a structured TypedError.
/// </summary>
public class PhotoForgeException : Exception
{
    public TypedError Error { get; }

    public PhotoForgeException(TypedError error, Exception? innerException = null)
        : base(error.UserMessage, innerException)
    {
        Error = error;
    }

    public PhotoForgeException(ErrorCategory category, string userMessage, string? diagnostic = null, Exception? innerException = null)
        : this(TypedError.Create(category, userMessage, diagnostic), innerException)
    {
    }
}
