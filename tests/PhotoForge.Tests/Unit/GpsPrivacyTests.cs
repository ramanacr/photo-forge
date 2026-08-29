using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PhotoForge.Core.Models;
using PhotoForge.Metadata;
using PhotoForge.Tests.Fixtures;
using Xunit;

namespace PhotoForge.Tests.Unit;

public class GpsPrivacyTests : IDisposable
{
    private readonly string _testDir;
    private readonly MetadataEngine _engine;

    public GpsPrivacyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PhotoForgeTests_Gps_" + Guid.NewGuid().ToString("N"));
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
    public async Task Merge_WithGpsRemovePolicy_ShouldCompletelyStripCoordinates()
    {
        var orig = TestPhotoFactory.CreateOriginalSample(_testDir, "orig.jpg");
        var edited = TestPhotoFactory.CreateEditedSample(_testDir, orig, "edited.jpg");

        var origMeta = await _engine.ExtractMetadataAsync(orig);
        var targetMeta = await _engine.ExtractMetadataAsync(edited);

        var profile = new MergeProfile { GpsPolicy = GpsPrivacyPolicy.Remove };
        var merged = _engine.MergeMetadata(origMeta, targetMeta, "src_fp_1", profile, out var diff);

        merged.Gps.Should().BeNull();
        merged.HasGps.Should().BeFalse();
        diff.Skipped.Should().Contain(s => s.Contains("GPS"));
    }

    [Fact]
    public async Task Merge_WithGpsRoundPolicy_ShouldRoundCoordinatesToTwoDecimals()
    {
        var orig = TestPhotoFactory.CreateOriginalSample(_testDir, "orig.jpg");
        var edited = TestPhotoFactory.CreateEditedSample(_testDir, orig, "edited.jpg");

        var origMeta = await _engine.ExtractMetadataAsync(orig);
        var targetMeta = await _engine.ExtractMetadataAsync(edited);

        var profile = new MergeProfile { GpsPolicy = GpsPrivacyPolicy.Round };
        var merged = _engine.MergeMetadata(origMeta, targetMeta, "src_fp_2", profile, out var diff);

        merged.Gps.Should().NotBeNull();
        merged.Gps!.Latitude.Should().Be(37.77);
        merged.Gps.Longitude.Should().Be(-122.42);
        diff.Warnings.Should().Contain(w => w.Contains("1km"));
    }

    [Fact]
    public async Task Merge_WithGpsCopyWithWarningPolicy_ShouldPreserveExactGpsAndLogWarning()
    {
        var orig = TestPhotoFactory.CreateOriginalSample(_testDir, "orig.jpg");
        var edited = TestPhotoFactory.CreateEditedSample(_testDir, orig, "edited.jpg");

        var origMeta = await _engine.ExtractMetadataAsync(orig);
        var targetMeta = await _engine.ExtractMetadataAsync(edited);

        var profile = new MergeProfile { GpsPolicy = GpsPrivacyPolicy.CopyWithWarning };
        var merged = _engine.MergeMetadata(origMeta, targetMeta, "src_fp_3", profile, out var diff);

        merged.Gps.Should().NotBeNull();
        merged.Gps!.Latitude.Should().BeApproximately(37.7749, 0.001);
        diff.Warnings.Should().Contain(w => w.Contains("exact GPS"));
    }
}
