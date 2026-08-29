using System;

namespace PhotoForge.Core.Models;

/// <summary>
/// Supported photo formats in the PhotoForge pipeline.
/// </summary>
public enum PhotoFormat
{
    Unknown = 0,
    Jpeg = 1,
    Png = 2,
    WebP = 3,
    Tiff = 4,
    Bmp = 5,
    Gif = 6,
    Avif = 7,
    Heic = 8,
    Heif = 9,
    Dng = 10
}

/// <summary>
/// Helper extensions for PhotoFormat characteristics.
/// </summary>
public static class PhotoFormatExtensions
{
    public static string GetDefaultExtension(this PhotoFormat format) => format switch
    {
        PhotoFormat.Jpeg => ".jpg",
        PhotoFormat.Png => ".png",
        PhotoFormat.WebP => ".webp",
        PhotoFormat.Tiff => ".tiff",
        PhotoFormat.Bmp => ".bmp",
        PhotoFormat.Gif => ".gif",
        PhotoFormat.Avif => ".avif",
        PhotoFormat.Heic => ".heic",
        PhotoFormat.Heif => ".heif",
        PhotoFormat.Dng => ".dng",
        _ => ".bin"
    };

    public static string GetMimeType(this PhotoFormat format) => format switch
    {
        PhotoFormat.Jpeg => "image/jpeg",
        PhotoFormat.Png => "image/png",
        PhotoFormat.WebP => "image/webp",
        PhotoFormat.Tiff => "image/tiff",
        PhotoFormat.Bmp => "image/bmp",
        PhotoFormat.Gif => "image/gif",
        PhotoFormat.Avif => "image/avif",
        PhotoFormat.Heic => "image/heic",
        PhotoFormat.Heif => "image/heif",
        PhotoFormat.Dng => "image/x-adobe-dng",
        _ => "application/octet-stream"
    };

    public static bool SupportsExifMetadata(this PhotoFormat format) => format switch
    {
        PhotoFormat.Jpeg or PhotoFormat.Png or PhotoFormat.WebP or PhotoFormat.Tiff 
            or PhotoFormat.Avif or PhotoFormat.Heic or PhotoFormat.Heif or PhotoFormat.Dng => true,
        _ => false
    };

    public static bool SupportsHeicConversion(this PhotoFormat format) => format switch
    {
        PhotoFormat.Jpeg or PhotoFormat.Png or PhotoFormat.WebP or PhotoFormat.Tiff 
            or PhotoFormat.Bmp or PhotoFormat.Gif or PhotoFormat.Avif or PhotoFormat.Dng => true,
        _ => false
    };

    public static bool IsHeicOrHeif(this PhotoFormat format) =>
        format is PhotoFormat.Heic or PhotoFormat.Heif;
}
