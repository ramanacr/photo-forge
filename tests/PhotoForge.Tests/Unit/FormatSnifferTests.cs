using System;
using System.IO;
using FluentAssertions;
using PhotoForge.Core.Models;
using PhotoForge.Imaging;
using Xunit;

namespace PhotoForge.Tests.Unit;

public class FormatSnifferTests
{
    [Fact]
    public void Sniff_ShouldIdentifyJpegHeader()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        using var ms = new MemoryStream(jpegBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.Jpeg);
    }

    [Fact]
    public void Sniff_ShouldIdentifyPngHeader()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        using var ms = new MemoryStream(pngBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.Png);
    }

    [Fact]
    public void Sniff_ShouldIdentifyGifHeader()
    {
        var gifBytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
        using var ms = new MemoryStream(gifBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.Gif);
    }

    [Fact]
    public void Sniff_ShouldIdentifyBmpHeader()
    {
        var bmpBytes = new byte[] { 0x42, 0x4D, 0x36, 0x00, 0x00, 0x00 };
        using var ms = new MemoryStream(bmpBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.Bmp);
    }

    [Fact]
    public void Sniff_ShouldIdentifyWebPHeader()
    {
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        using var ms = new MemoryStream(webpBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.WebP);
    }

    [Fact]
    public void Sniff_ShouldIdentifyTiffHeader()
    {
        var tiffBytes = new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00 };
        using var ms = new MemoryStream(tiffBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.Tiff);
    }

    [Fact]
    public void Sniff_ShouldIdentifyHeicHeader()
    {
        var heicBytes = new byte[]
        {
            0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, // ... ftyp
            0x68, 0x65, 0x69, 0x63, 0x00, 0x00, 0x00, 0x00  // heic ....
        };
        using var ms = new MemoryStream(heicBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.Heic);
    }

    [Fact]
    public void Sniff_ShouldIdentifyAvifHeader()
    {
        var avifBytes = new byte[]
        {
            0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70, // ... ftyp
            0x61, 0x76, 0x69, 0x66, 0x00, 0x00, 0x00, 0x00  // avif ....
        };
        using var ms = new MemoryStream(avifBytes);
        FormatSniffer.Sniff(ms).Should().Be(PhotoFormat.Avif);
    }
}
