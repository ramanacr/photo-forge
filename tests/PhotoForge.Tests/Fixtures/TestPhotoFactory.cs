using System;
using System.IO;
using PhotoForge.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoForge.Tests.Fixtures;

/// <summary>
/// Generates synthetic test photos with known metadata, GPS, colors, and edited variants.
/// </summary>
public static class TestPhotoFactory
{
    public static string CreateOriginalSample(string directory, string fileName = "IMG_4001.jpg", int width = 1200, int height = 800)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);

        using var image = new Image<Rgba32>(width, height);
        // Fill image with natural photographic regions (sky, mountain, foreground)
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    if (y < accessor.Height / 3)
                    {
                        // Sky with horizontal gradient
                        byte b = (byte)(200 + (x * 55 / pixelRow.Length));
                        pixelRow[x] = new Rgba32(80, 140, b, 255);
                    }
                    else if (y < (accessor.Height * 2) / 3)
                    {
                        // Mountain with contrasting slope
                        byte v = (byte)(40 + (x * 120 / pixelRow.Length));
                        pixelRow[x] = new Rgba32(v, (byte)(v / 2), 30, 255);
                    }
                    else
                    {
                        // Dark foreground
                        pixelRow[x] = new Rgba32(30, 90, 40, 255);
                    }
                }
            }
        });

        // Add EXIF capture metadata
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.DateTimeOriginal, "2025:06:15 14:30:00");
        exif.SetValue(ExifTag.DateTimeDigitized, "2025:06:15 14:30:00");
        exif.SetValue(ExifTag.Make, "Sony");
        exif.SetValue(ExifTag.Model, "ILCE-7RM5");
        exif.SetValue(ExifTag.LensModel, "FE 24-70mm F2.8 GM II");
        exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 100 });
        exif.SetValue(ExifTag.FNumber, new Rational(28, 10)); // f/2.8
        exif.SetValue(ExifTag.FocalLength, new Rational(50, 1)); // 50mm

        // Add GPS: 37.7749 N, 122.4194 W (San Francisco)
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, new Rational[] { new(37, 1), new(46, 1), new(29640, 1000) });
        exif.SetValue(ExifTag.GPSLongitudeRef, "W");
        exif.SetValue(ExifTag.GPSLongitude, new Rational[] { new(122, 1), new(25, 1), new(9840, 1000) });
        exif.SetValue(ExifTag.GPSAltitudeRef, (byte)0);
        exif.SetValue(ExifTag.GPSAltitude, new Rational(1500, 100)); // 15.0m

        image.Metadata.ExifProfile = exif;
        image.SaveAsJpeg(filePath);

        return filePath;
    }

    public static string CreateEditedSample(string directory, string originalPath, string fileName = "IMG_4001_edited.jpg", bool crop = false, bool stripMetadata = true)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);

        using var image = Image.Load<Rgba32>(originalPath);
        image.Mutate(ctx =>
        {
            if (crop)
            {
                // Mild 10% crop
                ctx.Crop(new Rectangle(50, 50, image.Width - 100, image.Height - 100));
            }
            // Resize slightly (e.g. exported for web)
            ctx.Resize(new Size(image.Width * 3 / 4, image.Height * 3 / 4));
            // Slight contrast adjust
            ctx.Contrast(1.1f);
        });

        if (stripMetadata)
        {
            // Simulate photo editor stripping EXIF/GPS, only adding software tag
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Software, "Adobe Photoshop 2026");
            image.Metadata.ExifProfile = exif;
        }

        image.SaveAsJpeg(filePath);
        return filePath;
    }

    public static string CreateUnrelatedSample(string directory, string fileName = "IMG_9999_unrelated.jpg")
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);

        using var image = new Image<Rgba32>(800, 800);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    byte r = (byte)(255 - (x * 255 / pixelRow.Length));
                    byte g = (byte)(255 - (y * 255 / accessor.Height));
                    pixelRow[x] = new Rgba32(r, g, 128, 255);
                }
            }
        });

        var exif = new ExifProfile();
        exif.SetValue(ExifTag.DateTimeOriginal, "2020:01:01 00:00:00");
        exif.SetValue(ExifTag.Make, "Canon");
        exif.SetValue(ExifTag.Model, "Canon EOS R5");
        image.Metadata.ExifProfile = exif;

        image.SaveAsJpeg(filePath);
        return filePath;
    }
}
