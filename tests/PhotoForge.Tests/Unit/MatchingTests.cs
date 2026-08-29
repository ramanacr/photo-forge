using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;
using PhotoForge.Imaging;
using PhotoForge.Matching;
using PhotoForge.Matching.Signals;
using PhotoForge.Metadata;
using PhotoForge.Storage;
using PhotoForge.Tests.Fixtures;
using Xunit;

namespace PhotoForge.Tests.Unit;

public class MatchingTests : IDisposable
{
    private readonly string _testDir;
    private readonly IImageEngine _imageEngine;
    private readonly MatchingEngine _matchingEngine;
    private readonly MetadataEngine _metaEngine;
    private readonly StorageEngine _storageEngine;

    public MatchingTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PhotoForgeTests_Match_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _imageEngine = new ImageEngine();
        _matchingEngine = new MatchingEngine(_imageEngine);
        _metaEngine = new MetadataEngine();
        _storageEngine = new StorageEngine();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public void FilenameSignal_ShouldScoreHigh_WhenStandardEditSuffixesAreUsed()
    {
        var s1 = FilenameSignal.Evaluate("IMG_1234_edited.jpg", "IMG_1234.jpg", out var r1);
        s1.Should().BeGreaterOrEqualTo(0.95);
        r1.Should().NotBeNull();

        var s2 = FilenameSignal.Evaluate("DSC_0050-final-copy.jpg", "DSC_0050.jpg", out var r2);
        s2.Should().BeGreaterOrEqualTo(0.95);

        var s3 = FilenameSignal.Evaluate("Photo (1).jpg", "Photo.jpg", out var r3);
        s3.Should().BeGreaterOrEqualTo(0.95);

        var sUnrelated = FilenameSignal.Evaluate("sunset_vacation.jpg", "office_meeting.jpg", out _);
        sUnrelated.Should().BeLessThan(0.40);
    }

    [Fact]
    public async Task MatchingEngine_ShouldFindCorrectOriginal_WithHighConfidence()
    {
        var origPath = TestPhotoFactory.CreateOriginalSample(_testDir, "IMG_4001.jpg");
        var editedPath = TestPhotoFactory.CreateEditedSample(_testDir, origPath, "IMG_4001_edited.jpg");
        var unrelatedPath = TestPhotoFactory.CreateUnrelatedSample(_testDir, "IMG_9999_unrelated.jpg");

        var origRef = PhotoRef.Create(
            origPath,
            PhotoFormat.Jpeg,
            new FileInfo(origPath).Length,
            await _storageEngine.ComputeFileSha256Async(origPath),
            await _imageEngine.InspectDimensionsAsync(origPath),
            metadata: await _metaEngine.ExtractMetadataAsync(origPath),
            perceptualHash: await _imageEngine.ComputePerceptualHashAsync(origPath));

        var unrelatedRef = PhotoRef.Create(
            unrelatedPath,
            PhotoFormat.Jpeg,
            new FileInfo(unrelatedPath).Length,
            await _storageEngine.ComputeFileSha256Async(unrelatedPath),
            await _imageEngine.InspectDimensionsAsync(unrelatedPath),
            metadata: await _metaEngine.ExtractMetadataAsync(unrelatedPath),
            perceptualHash: await _imageEngine.ComputePerceptualHashAsync(unrelatedPath));

        var targetRef = PhotoRef.Create(
            editedPath,
            PhotoFormat.Jpeg,
            new FileInfo(editedPath).Length,
            await _storageEngine.ComputeFileSha256Async(editedPath),
            await _imageEngine.InspectDimensionsAsync(editedPath),
            metadata: await _metaEngine.ExtractMetadataAsync(editedPath),
            perceptualHash: await _imageEngine.ComputePerceptualHashAsync(editedPath));

        var candidates = await _matchingEngine.FindCandidatesAsync(targetRef, new[] { origRef, unrelatedRef });

        candidates.Should().HaveCount(2);
        var best = candidates[0];

        best.CandidateRef.FilePath.Should().Be(origPath);
        best.Score.Should().BeGreaterOrEqualTo(0.85);
        best.Band.Should().BeOneOf(ConfidenceBand.AutoAccept, ConfidenceBand.Suggested);
        best.Reasons.Should().NotBeEmpty();

        var worst = candidates[1];
        worst.CandidateRef.FilePath.Should().Be(unrelatedPath);
        worst.Score.Should().BeLessThan(0.60);
        worst.Band.Should().Be(ConfidenceBand.NoMatch);
    }
}
