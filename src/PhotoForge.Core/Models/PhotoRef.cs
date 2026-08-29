using System;

namespace PhotoForge.Core.Models;

/// <summary>
/// Dimensions of an image in pixels.
/// </summary>
public readonly record struct ImageDimensions(int Width, int Height)
{
    public double AspectRatio => Height == 0 ? 0.0 : (double)Width / Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>
/// Immutable snapshot reference to a photo file and its physical/structural properties.
/// </summary>
public record PhotoRef
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required PhotoFormat Format { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string Sha256Fingerprint { get; init; }
    public ImageDimensions Dimensions { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? ModifiedAtUtc { get; init; }
    public MetadataDocument? Metadata { get; init; }
    public ulong? PerceptualHash { get; init; }

    public static PhotoRef Create(
        string filePath,
        PhotoFormat format,
        long fileSizeBytes,
        string sha256Fingerprint,
        ImageDimensions dimensions = default,
        DateTime? createdAtUtc = null,
        DateTime? modifiedAtUtc = null,
        MetadataDocument? metadata = null,
        ulong? perceptualHash = null)
    {
        return new PhotoRef
        {
            FilePath = filePath,
            FileName = System.IO.Path.GetFileName(filePath),
            Format = format,
            FileSizeBytes = fileSizeBytes,
            Sha256Fingerprint = sha256Fingerprint,
            Dimensions = dimensions,
            CreatedAtUtc = createdAtUtc,
            ModifiedAtUtc = modifiedAtUtc,
            Metadata = metadata,
            PerceptualHash = perceptualHash
        };
    }
}
