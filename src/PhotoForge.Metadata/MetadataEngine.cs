using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;
using PhotoForge.Metadata.Mergers;
using PhotoForge.Metadata.Parsers;

namespace PhotoForge.Metadata;

/// <summary>
/// Core implementation of IMetadataEngine for metadata parsing, diffing, and merging.
/// </summary>
public class MetadataEngine : IMetadataEngine
{
    private readonly MetadataParser _parser = new();
    private readonly MetadataMerger _merger = new();

    public async Task<MetadataDocument> ExtractMetadataAsync(string filePath, CancellationToken ct = default)
    {
        return await _parser.ParseFileAsync(filePath, ct);
    }

    public async Task<MetadataDocument> ExtractMetadataAsync(Stream stream, PhotoFormat format, CancellationToken ct = default)
    {
        return await _parser.ParseStreamAsync(stream, format, ct);
    }

    public MetadataDiff ComputeDiff(MetadataDocument original, MetadataDocument target, MergeProfile profile)
    {
        _merger.Merge(original, target, "diff_preview", profile, out var diff);
        return diff;
    }

    public MetadataDocument MergeMetadata(MetadataDocument original, MetadataDocument target, string sourceFingerprint, MergeProfile profile, out MetadataDiff diff)
    {
        return _merger.Merge(original, target, sourceFingerprint, profile, out diff);
    }

    public async Task InjectMetadataAsync(Stream inputImage, Stream outputImage, MetadataDocument mergedMetadata, PhotoFormat format, CancellationToken ct = default)
    {
        // Copy stream safely; container injection is integrated in the image pipeline
        await inputImage.CopyToAsync(outputImage, 81920, ct);
    }
}
