using System;
using System.IO;
using PhotoForge.Core.Models;

namespace PhotoForge.Imaging;

/// <summary>
/// Deterministic binary format sniffer detecting formats by file signature rather than extension alone.
/// </summary>
public static class FormatSniffer
{
    public static PhotoFormat Sniff(string filePath)
    {
        if (!File.Exists(filePath))
            return PhotoFormat.Unknown;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Sniff(stream);
        }
        catch
        {
            return PhotoFormat.Unknown;
        }
    }

    public static PhotoFormat Sniff(Stream stream)
    {
        if (stream == null || !stream.CanRead)
            return PhotoFormat.Unknown;

        long initialPos = stream.CanSeek ? stream.Position : 0;
        try
        {
            var header = new byte[64];
            int read = stream.Read(header, 0, header.Length);
            if (read < 4)
                return PhotoFormat.Unknown;

            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return PhotoFormat.Jpeg;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (read >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return PhotoFormat.Png;

            // GIF: 47 49 46 38 ("GIF8")
            if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                return PhotoFormat.Gif;

            // BMP: 42 4D ("BM")
            if (header[0] == 0x42 && header[1] == 0x4D)
                return PhotoFormat.Bmp;

            // WebP: RIFF ... WEBP
            if (read >= 12 &&
                header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return PhotoFormat.WebP;

            // TIFF: II*. (49 49 2A 00) or MM.* (4D 4D 00 2A)
            if ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
                (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A))
            {
                return PhotoFormat.Tiff;
            }

            // ISO Base Media File Format (HEIC, HEIF, AVIF): ... ftyp[brand]
            if (read >= 16 &&
                header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) // 'ftyp'
            {
                string brand = System.Text.Encoding.ASCII.GetString(header, 8, 4).ToLowerInvariant();
                if (brand is "heic" or "heix" or "heim" or "heis" or "mif1" or "msf1")
                    return PhotoFormat.Heic;
                if (brand is "avif" or "avis")
                    return PhotoFormat.Avif;
            }

            return PhotoFormat.Unknown;
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = initialPos;
        }
    }
}
