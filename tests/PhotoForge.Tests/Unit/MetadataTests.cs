using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PhotoForge.Core.Models;
using PhotoForge.Metadata;
using PhotoForge.Metadata.Markers;
using PhotoForge.Tests.Fixtures;
using Xunit;

namespace PhotoForge.Tests.Unit;

public class MetadataTests : IDisposable
{
    private readonly string _testDir;
    private readonly MetadataEngine _engine;

    public MetadataTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PhotoForgeTests_Meta_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _engine = new MetadataEngine();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ExtractMetadata_ShouldParseExifAndGps_WhenPresent()
    {
        var samplePath = TestPhotoFactory.CreateOriginalSample(_testDir, "sample_orig.jpg");

        var meta = await _engine.ExtractMetadataAsync(samplePath);

        meta.Should().NotBeNull();
        meta.Exif.Camera.Make.Should().Be("Sony");
        meta.Exif.Camera.Model.Should().Be("ILCE-7RM5");
        meta.Exif.DateTimeOriginal.Should().HaveValue();
        meta.Exif.DateTimeOriginal!.Value.Year.Should().Be(2025);
        meta.Gps.Should().NotBeNull();
        meta.Gps!.Latitude.Should().BeApproximately(37.7749, 0.01);
        meta.Gps.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }

    [Fact]
    public async Task MergeMetadata_ShouldPreserveOriginalProvenanceAndTargetEditState()
    {
        var origPath = TestPhotoFactory.CreateOriginalSample(_testDir, "orig.jpg");
        var editedPath = TestPhotoFactory.CreateEditedSample(_testDir, origPath, "edited.jpg");

        var origMeta = await _engine.ExtractMetadataAsync(origPath);
        var targetMeta = await _engine.ExtractMetadataAsync(editedPath);

        var merged = _engine.MergeMetadata(origMeta, targetMeta, "test_source_fp", MergeProfile.StandardV1, out var diff);

        merged.Exif.Camera.Model.Should().Be("ILCE-7RM5");
        merged.Exif.DateTimeOriginal.Should().Be(origMeta.Exif.DateTimeOriginal);
        merged.Exif.Camera.Software.Should().Be("Adobe Photoshop 2026");
        merged.Gps.Should().NotBeNull();
        merged.Marker.Should().NotBeNull();
        merged.Marker!.Processed.Should().BeTrue();

        diff.CopiedFromOriginal.Should().Contain(c => c.Contains("DateTimeOriginal"));
        diff.CopiedFromOriginal.Should().Contain(c => c.Contains("GPS.Latitude"));
        diff.PreservedFromTarget.Should().Contain(p => p.Contains("Software"));
    }

    [Fact]
    public void MigrationMarker_ShouldRoundTripCorrectly()
    {
        var marker = new MigrationMarker
        {
            Processed = true,
            SourceFingerprint = "abcd1234efgh5678",
            Profile = "standard-v1",
            MigrationVersion = 1,
            EngineVersion = "1.0.0",
            ProcessedAtUtc = DateTime.UtcNow
        };

        var markerStr = marker.ToMarkerString();
        var success = MigrationMarker.TryParse(markerStr, out var parsed);

        success.Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.SourceFingerprint.Should().Be(marker.SourceFingerprint);
        parsed.Profile.Should().Be(marker.Profile);
        parsed.MigrationVersion.Should().Be(1);
    }
}
