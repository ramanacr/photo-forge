using System;
using System.Collections.Generic;

namespace PhotoForge.Core.Models;

/// <summary>
/// GPS coordinate representation with latitude, longitude, altitude, and precision.
/// </summary>
public record GpsCoordinate
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double? AltitudeMeters { get; init; }
    public double? DirectionDegrees { get; init; }
    public double? SpeedKmH { get; init; }
    public double? DilutionOfPrecision { get; init; }
    public string? ProcessingMethod { get; init; }
    public DateTime? TimestampUtc { get; init; }

    public override string ToString() =>
        AltitudeMeters.HasValue
            ? $"{Latitude:F6}, {Longitude:F6} ({AltitudeMeters.Value:F1}m)"
            : $"{Latitude:F6}, {Longitude:F6}";
}

/// <summary>
/// Camera and lens optical equipment information.
/// </summary>
public record CameraInfo
{
    public string? Make { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public string? LensMake { get; init; }
    public string? LensModel { get; init; }
    public string? LensSerialNumber { get; init; }
    public string? Software { get; init; }
    public string? HostComputer { get; init; }
}

/// <summary>
/// Photographic exposure and camera sensor settings.
/// </summary>
public record ExposureInfo
{
    public int? Iso { get; init; }
    public double? ExposureTimeSeconds { get; init; }
    public double? FNumber { get; init; }
    public double? FocalLengthMm { get; init; }
    public double? FocalLengthIn35MmFilm { get; init; }
    public string? ExposureProgram { get; init; }
    public string? MeteringMode { get; init; }
    public string? Flash { get; init; }
    public string? WhiteBalance { get; init; }
    public double? ExposureBiasValue { get; init; }
    public string? ColorSpace { get; init; }
}

/// <summary>
/// Standard EXIF tags container.
/// </summary>
public record ExifData
{
    public DateTime? DateTimeOriginal { get; init; }
    public DateTime? CreateDate { get; init; }
    public DateTime? ModifyDate { get; init; }
    public string? OffsetTimeOriginal { get; init; }
    public string? SubSecTimeOriginal { get; init; }
    public int? Orientation { get; init; }
    public CameraInfo Camera { get; init; } = new();
    public ExposureInfo Exposure { get; init; } = new();
    public string? UserComment { get; init; }
    public string? ImageDescription { get; init; }
    public string? Copyright { get; init; }
    public string? Artist { get; init; }
    public byte[]? RawMakerNotes { get; init; }
    public Dictionary<string, string> AdditionalTags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// IPTC-NAA standard metadata fields.
/// </summary>
public record IptcData
{
    public string? Title { get; init; }
    public string? Caption { get; init; }
    public string? Byline { get; init; }
    public string? BylineTitle { get; init; }
    public string? CopyrightNotice { get; init; }
    public string? Credit { get; init; }
    public string? Source { get; init; }
    public string? ObjectName { get; init; }
    public string? City { get; init; }
    public string? ProvinceState { get; init; }
    public string? Country { get; init; }
    public string? CountryCode { get; init; }
    public DateTime? DateCreated { get; init; }
    public List<string> Keywords { get; init; } = new();
    public Dictionary<string, string> AdditionalTags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Adobe Extensible Metadata Platform (XMP) container.
/// </summary>
public record XmpData
{
    public string? RawXml { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SubjectKeywords { get; init; } = new();
    public string? Description { get; init; }
    public string? Creator { get; init; }
    public string? Rights { get; init; }
}

/// <summary>
/// ICC Color Profile container.
/// </summary>
public record IccProfileData
{
    public string? ProfileName { get; init; }
    public string? ColorSpace { get; init; }
    public byte[]? RawBytes { get; init; }
}

/// <summary>
/// PhotoForge Idempotency Migration Marker.
/// Written to targets to record migration provenance and skip repeated operations.
/// </summary>
public record MigrationMarker
{
    public const string NamespaceUri = "http://photoforge.example/ns/1.0/";
    public const string MarkerPrefix = "PF-MIG";
    public const int CurrentSchemaVersion = 1;

    public bool Processed { get; init; } = true;
    public required string SourceFingerprint { get; init; }
    public string Profile { get; init; } = "standard-v1";
    public int MigrationVersion { get; init; } = CurrentSchemaVersion;
    public string EngineVersion { get; init; } = "1.0.0";
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;

    public string ToMarkerString() =>
        $"{MarkerPrefix}|v={MigrationVersion}|src={SourceFingerprint}|prof={Profile}|eng={EngineVersion}|ts={ProcessedAtUtc:O}";

    public static bool TryParse(string raw, out MigrationMarker? marker)
    {
        marker = null;
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith(MarkerPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var parts = raw.Split('|');
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < parts.Length; i++)
            {
                var kv = parts[i].Split(new[] { '=' }, 2);
                if (kv.Length == 2)
                    dict[kv[0]] = kv[1];
            }

            if (!dict.TryGetValue("src", out var src) || string.IsNullOrWhiteSpace(src))
                return false;

            dict.TryGetValue("v", out var vStr);
            int.TryParse(vStr, out var v);
            if (v == 0) v = 1;

            dict.TryGetValue("prof", out var prof);
            dict.TryGetValue("eng", out var eng);
            dict.TryGetValue("ts", out var tsStr);
            DateTime.TryParse(tsStr, out var ts);
            if (ts == default) ts = DateTime.UtcNow;

            marker = new MigrationMarker
            {
                Processed = true,
                SourceFingerprint = src,
                Profile = prof ?? "standard-v1",
                MigrationVersion = v,
                EngineVersion = eng ?? "1.0.0",
                ProcessedAtUtc = ts
            };
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Strongly-typed canonical representation of all image metadata.
/// </summary>
public record MetadataDocument
{
    public ExifData Exif { get; init; } = new();
    public GpsCoordinate? Gps { get; init; }
    public IptcData Iptc { get; init; } = new();
    public XmpData Xmp { get; init; } = new();
    public IccProfileData? IccProfile { get; init; }
    public MigrationMarker? Marker { get; init; }
    public Dictionary<string, string> RawTags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasGps => Gps != null && (Math.Abs(Gps.Latitude) > 0.000001 || Math.Abs(Gps.Longitude) > 0.000001);
    public bool HasCaptureDate => Exif.DateTimeOriginal.HasValue || Exif.CreateDate.HasValue;
    public DateTime? BestCaptureDate => Exif.DateTimeOriginal ?? Exif.CreateDate ?? Iptc.DateCreated;
    public string? CameraMakeAndModel =>
        !string.IsNullOrWhiteSpace(Exif.Camera.Make) && !string.IsNullOrWhiteSpace(Exif.Camera.Model)
            ? $"{Exif.Camera.Make} {Exif.Camera.Model}".Trim()
            : Exif.Camera.Model ?? Exif.Camera.Make;
}
