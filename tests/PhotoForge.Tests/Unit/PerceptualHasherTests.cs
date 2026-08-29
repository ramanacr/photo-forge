using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using PhotoForge.Imaging;
using PhotoForge.Tests.Fixtures;
using Xunit;

namespace PhotoForge.Tests.Unit;

public class PerceptualHasherTests : IDisposable
{
    private readonly string _testDir;
    private readonly ImageEngine _imageEngine;

    public PerceptualHasherTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PhotoForgeTests_Phash_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _imageEngine = new ImageEngine();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task PerceptualHash_ShouldYieldHighSimilarity_BetweenOriginalAndEditedVariants()
    {
        var orig = TestPhotoFactory.CreateOriginalSample(_testDir, "orig.jpg");
        var edited = TestPhotoFactory.CreateEditedSample(_testDir, orig, "edited.jpg", crop: true);
        var unrelated = TestPhotoFactory.CreateUnrelatedSample(_testDir, "unrelated.jpg");

        var origHash = await _imageEngine.ComputePerceptualHashAsync(orig);
        var editedHash = await _imageEngine.ComputePerceptualHashAsync(edited);
        var unrelatedHash = await _imageEngine.ComputePerceptualHashAsync(unrelated);

        origHash.Should().NotBe(0);
        editedHash.Should().NotBe(0);

        var similarityOriginalToEdited = _imageEngine.ComparePerceptualHashes(origHash, editedHash);
        var similarityOriginalToUnrelated = _imageEngine.ComparePerceptualHashes(origHash, unrelatedHash);

        similarityOriginalToEdited.Should().BeGreaterOrEqualTo(0.80);
        similarityOriginalToUnrelated.Should().BeLessThan(0.70);
    }
}
