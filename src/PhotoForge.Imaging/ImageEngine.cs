using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using PhotoForge.Core.Models;
using PhotoForge.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoForge.Imaging;

/// <summary>
/// High-performance image inspection, perceptual hashing, format encoding/decoding, and metadata preservation.
/// </summary>
public class ImageEngine : IImageEngine
{
    public PhotoFormat SniffFormat(Stream stream) => FormatSniffer.Sniff(stream);

    public PhotoFormat SniffFormat(string filePath) => FormatSniffer.Sniff(filePath);

    public async Task<ImageDimensions> InspectDimensionsAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return default;

        return await Task.Run(() =>
        {
            try
            {
                var info = Image.Identify(filePath);
                return info != null ? new ImageDimensions(info.Width, info.Height) : default;
            }
            catch
            {
                return default;
            }
        }, ct);
    }

    public async Task<ulong> ComputePerceptualHashAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return 0;

        return await Task.Run(() =>
        {
            try
            {
                using var image = Image.Load<Rgba32>(filePath);
                return ComputeDHash(image);
            }
            catch
            {
                return 0UL;
            }
        }, ct);
    }

    public async Task<ulong> ComputePerceptualHashAsync(Stream stream, PhotoFormat format, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                long pos = stream.CanSeek ? stream.Position : 0;
                using var image = Image.Load<Rgba32>(stream);
                if (stream.CanSeek)
                    stream.Position = pos;
                return ComputeDHash(image);
            }
            catch
            {
                return 0UL;
            }
        }, ct);
    }

    private static ulong ComputeDHash(Image<Rgba32> source)
    {
        // 1. Resize to 9x8 grayscale
        using var clone = source.Clone(ctx => ctx
            .Resize(new ResizeOptions
            {
                Size = new Size(9, 8),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic
            })
            .Grayscale());

        ulong hash = 0;
        int bitIndex = 0;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var left = clone[x, y].R;
                var right = clone[x + 1, y].R;

                if (left > right)
                {
                    hash |= (1UL << bitIndex);
                }
                bitIndex++;
            }
        }

        return hash;
    }

    public double ComparePerceptualHashes(ulong hash1, ulong hash2)
    {
        if (hash1 == 0 && hash2 == 0) return 0.0;
        ulong xor = hash1 ^ hash2;
        int diffBits = BitOperations.PopCount(xor);
        return Math.Max(0.0, 1.0 - ((double)diffBits / 64.0));
    }

    public async Task<byte[]> GenerateThumbnailAsync(string filePath, int maxWidth = 256, int maxHeight = 256, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var image = Image.Load(filePath);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Max
                }));

                using var ms = new MemoryStream();
                image.SaveAsJpeg(ms, new JpegEncoder { Quality = 80 });
                return ms.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }, ct);
    }

    public async Task ConvertToHeicAsync(
        string sourcePath,
        string destinationPath,
        MetadataDocument? metadataToInject = null,
        ConversionQuality quality = ConversionQuality.High,
        CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            using var image = Image.Load(sourcePath);

            // Inject EXIF/IPTC metadata into ImageSharp image structure
            if (metadataToInject != null)
            {
                ApplyMetadataToImage(image, metadataToInject);
            }

            int qualityValue = quality switch
            {
                ConversionQuality.LosslessWhereSupported => 100,
                ConversionQuality.VeryHigh => 95,
                ConversionQuality.High => 85,
                ConversionQuality.Balanced => 75,
                ConversionQuality.Small => 60,
                _ => 85
            };

            var ext = Path.GetExtension(destinationPath).ToLowerInvariant();
            if (ext == ".webp")
            {
                var encoder = new WebpEncoder
                {
                    Quality = qualityValue,
                    FileFormat = quality == ConversionQuality.LosslessWhereSupported ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy
                };
                image.Save(destinationPath, encoder);
            }
            else if (ext == ".png")
            {
                image.SaveAsPng(destinationPath);
            }
            else
            {
                // Default high quality output container
                var encoder = new JpegEncoder
                {
                    Quality = qualityValue
                };
                image.Save(destinationPath, encoder);
            }
        }, ct);
    }

    public static void ApplyMetadataToImage(Image image, MetadataDocument doc)
    {
        var exif = image.Metadata.ExifProfile ?? new ExifProfile();

        if (doc.Exif.DateTimeOriginal.HasValue)
            exif.SetValue(ExifTag.DateTimeOriginal, doc.Exif.DateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss"));

        if (doc.Exif.CreateDate.HasValue)
            exif.SetValue(ExifTag.DateTimeDigitized, doc.Exif.CreateDate.Value.ToString("yyyy:MM:dd HH:mm:ss"));

        if (!string.IsNullOrEmpty(doc.Exif.Camera.Make))
            exif.SetValue(ExifTag.Make, doc.Exif.Camera.Make);

        if (!string.IsNullOrEmpty(doc.Exif.Camera.Model))
            exif.SetValue(ExifTag.Model, doc.Exif.Camera.Model);

        if (!string.IsNullOrEmpty(doc.Exif.Camera.Software))
            exif.SetValue(ExifTag.Software, doc.Exif.Camera.Software);

        if (doc.Exif.Exposure.Iso.HasValue)
            exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { (ushort)doc.Exif.Exposure.Iso.Value });

        if (doc.Exif.Exposure.FNumber.HasValue)
            exif.SetValue(ExifTag.FNumber, new Rational((uint)(doc.Exif.Exposure.FNumber.Value * 10), 10));

        if (doc.Exif.Exposure.FocalLengthMm.HasValue)
            exif.SetValue(ExifTag.FocalLength, new Rational((uint)(doc.Exif.Exposure.FocalLengthMm.Value * 10), 10));

        if (doc.Marker != null)
        {
            var markerStr = doc.Marker.ToMarkerString();
            exif.SetValue(ExifTag.UserComment, markerStr);
            exif.SetValue(ExifTag.ImageDescription, markerStr);
        }

        // GPS tags
        if (doc.Gps != null)
        {
            double lat = doc.Gps.Latitude;
            double lon = doc.Gps.Longitude;

            string latRef = lat >= 0 ? "N" : "S";
            string lonRef = lon >= 0 ? "E" : "W";

            lat = Math.Abs(lat);
            lon = Math.Abs(lon);

            uint latDeg = (uint)lat;
            double latMinRemainder = (lat - latDeg) * 60;
            uint latMin = (uint)latMinRemainder;
            double latSec = (latMinRemainder - latMin) * 60;

            uint lonDeg = (uint)lon;
            double lonMinRemainder = (lon - lonDeg) * 60;
            uint lonMin = (uint)lonMinRemainder;
            double lonSec = (lonMinRemainder - lonMin) * 60;

            exif.SetValue(ExifTag.GPSLatitudeRef, latRef);
            exif.SetValue(ExifTag.GPSLatitude, new Rational[]
            {
                new Rational(latDeg, 1),
                new Rational(latMin, 1),
                new Rational((uint)(latSec * 1000), 1000)
            });

            exif.SetValue(ExifTag.GPSLongitudeRef, lonRef);
            exif.SetValue(ExifTag.GPSLongitude, new Rational[]
            {
                new Rational(lonDeg, 1),
                new Rational(lonMin, 1),
                new Rational((uint)(lonSec * 1000), 1000)
            });

            if (doc.Gps.AltitudeMeters.HasValue)
            {
                byte altRef = (byte)(doc.Gps.AltitudeMeters.Value >= 0 ? 0 : 1);
                exif.SetValue(ExifTag.GPSAltitudeRef, altRef);
                exif.SetValue(ExifTag.GPSAltitude, new Rational((uint)(Math.Abs(doc.Gps.AltitudeMeters.Value) * 100), 100));
            }
        }

        image.Metadata.ExifProfile = exif;
    }
}
