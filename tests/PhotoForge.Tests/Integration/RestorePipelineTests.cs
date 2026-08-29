using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PhotoForge.Core.Models;
using PhotoForge.Core.Pipeline;
using PhotoForge.Imaging;
using PhotoForge.Matching;
using PhotoForge.Metadata;
using PhotoForge.Storage;
using PhotoForge.Storage.Database;
using PhotoForge.Tests.Fixtures;
using Xunit;

namespace PhotoForge.Tests.Integration;

public class RestorePipelineTests : IDisposable
{
    private readonly string _testDir;
    private readonly MetadataEngine _metaEngine;
    private readonly ImageEngine _imageEngine;
    private readonly MatchingEngine _matchingEngine;
    private readonly StorageEngine _storageEngine;
    private readonly AuditDatabase _auditRepo;
    private readonly PhotoForgePipeline _pipeline;

    public RestorePipelineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PhotoForgeTests_Pipe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _metaEngine = new MetadataEngine();
        _imageEngine = new ImageEngine();
        _matchingEngine = new MatchingEngine(_imageEngine);
        _storageEngine = new StorageEngine();
        _auditRepo = new AuditDatabase(Path.Combine(_testDir, "test_audit.db"));

        _pipeline = new PhotoForgePipeline(_metaEngine, _matchingEngine, _imageEngine, _storageEngine, _auditRepo);
    }

    public void Dispose()
    {
        _auditRepo.Dispose();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ProcessSinglePair_ShouldRestoreMetadata_AndProduceValidOutput()
    {
        var orig = TestPhotoFactory.CreateOriginalSample(_testDir, "orig.jpg");
        var edited = TestPhotoFactory.CreateEditedSample(_testDir, orig, "edited.jpg");
        var output = Path.Combine(_testDir, "restored_output.jpg");

        var result = await _pipeline.ProcessSinglePairAsync(orig, edited, output, MergeProfile.StandardV1);

        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(OperationStatus.Success, OperationStatus.SuccessWithWarnings);
        result.OutputPath.Should().Be(output);
        File.Exists(output).Should().BeTrue();

        // Verify metadata on output file
        var outMeta = await _metaEngine.ExtractMetadataAsync(output);
        outMeta.Exif.Camera.Make.Should().Be("Sony");
        outMeta.Exif.Camera.Model.Should().Be("ILCE-7RM5");
        outMeta.Exif.DateTimeOriginal.Should().HaveValue();
        outMeta.Gps.Should().NotBeNull();
        outMeta.Marker.Should().NotBeNull();
        outMeta.Marker!.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessBatch_ShouldHandleMultipleFiles_AndIndexCandidates()
    {
        var origDir = Path.Combine(_testDir, "Originals");
        var editedDir = Path.Combine(_testDir, "Edited");
        var outDir = Path.Combine(_testDir, "Output");

        Directory.CreateDirectory(origDir);
        Directory.CreateDirectory(editedDir);

        var orig1 = TestPhotoFactory.CreateOriginalSample(origDir, "IMG_1001.jpg");
        var orig2 = TestPhotoFactory.CreateOriginalSample(origDir, "IMG_1002.jpg");

        var edited1 = TestPhotoFactory.CreateEditedSample(editedDir, orig1, "IMG_1001_edit.jpg");
        var edited2 = TestPhotoFactory.CreateEditedSample(editedDir, orig2, "IMG_1002_edit.jpg");

        var summary = await _pipeline.ProcessBatchAsync(
            new[] { edited1, edited2 },
            new[] { orig1, orig2 },
            outDir,
            MergeProfile.StandardV1,
            autoAcceptConfidentMatches: true);

        summary.TotalItems.Should().Be(2);
        summary.SucceededCount.Should().Be(2);
        summary.FailedCount.Should().Be(0);

        Directory.GetFiles(outDir).Should().HaveCount(2);
    }
}
