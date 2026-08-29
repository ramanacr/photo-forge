using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Xmp;
using PhotoForge.Core.Models;
using PhotoForge.Metadata.Markers;
using Directory = MetadataExtractor.Directory;

namespace PhotoForge.Metadata.Parsers;

/// <summary>
/// High-fidelity parser extracting EXIF, GPS, IPTC, XMP, ICC, and migration markers.
/// </summary>
public class MetadataParser
{
    public async Task<MetadataDocument> ParseFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        return await Task.Run(() =>
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var directories = ImageMetadataReader.ReadMetadata(stream);
                return BuildMetadataDocument(directories);
            }
            catch (Exception ex)
            {
                // Fallback for minimal metadata when partial parse fails
                var doc = new MetadataDocument();
                doc.RawTags["ParserWarning"] = ex.Message;
                return doc;
            }
        }, ct);
    }

    public async Task<MetadataDocument> ParseStreamAsync(Stream stream, PhotoFormat format, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                long initialPos = stream.CanSeek ? stream.Position : 0;
                var directories = ImageMetadataReader.ReadMetadata(stream);
                if (stream.CanSeek)
                    stream.Position = initialPos;

                return BuildMetadataDocument(directories);
            }
            catch (Exception ex)
            {
                var doc = new MetadataDocument();
                doc.RawTags["ParserWarning"] = ex.Message;
                return doc;
            }
        }, ct);
    }

    private static MetadataDocument BuildMetadataDocument(IReadOnlyList<Directory> directories)
    {
        var rawTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in directories)
        {
            foreach (var tag in dir.Tags)
            {
                var key = $"{dir.Name}:{tag.Name}";
                var desc = tag.Description ?? "";
                rawTags[key] = desc;
            }
        }

        var exifData = ExtractExif(directories, rawTags);
        var gps = ExtractGps(directories);
        var iptc = ExtractIptc(directories);
        var xmp = ExtractXmp(directories);
        var icc = ExtractIcc(directories);
        var marker = ExtractMarker(directories, xmp, exifData);

        return new MetadataDocument
        {
            Exif = exifData,
            Gps = gps,
            Iptc = iptc,
            Xmp = xmp,
            IccProfile = icc,
            Marker = marker,
            RawTags = rawTags
        };
    }

    private static ExifData ExtractExif(IReadOnlyList<Directory> directories, Dictionary<string, string> rawTags)
    {
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

        DateTime? dtOriginal = null;
        DateTime? dtCreate = null;
        DateTime? dtModify = null;

        if (subIfd != null)
        {
            if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dto))
                dtOriginal = dto;
            if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var dtc))
                dtCreate = dtc;
        }

        if (ifd0 != null)
        {
            if (ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dtm))
                dtModify = dtm;
            if (!dtOriginal.HasValue && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var fallbackDto))
                dtOriginal = fallbackDto;
        }

        // Camera Info
        var camera = new CameraInfo
        {
            Make = ifd0?.GetString(ExifDirectoryBase.TagMake)?.Trim(),
            Model = ifd0?.GetString(ExifDirectoryBase.TagModel)?.Trim(),
            Software = ifd0?.GetString(ExifDirectoryBase.TagSoftware)?.Trim(),
            HostComputer = ifd0?.GetString(ExifDirectoryBase.TagHostComputer)?.Trim(),
            LensMake = subIfd?.GetString(ExifDirectoryBase.TagLensMake)?.Trim(),
            LensModel = subIfd?.GetString(ExifDirectoryBase.TagLensModel)?.Trim(),
            LensSerialNumber = subIfd?.GetString(ExifDirectoryBase.TagLensSerialNumber)?.Trim(),
            SerialNumber = subIfd?.GetString(ExifDirectoryBase.TagBodySerialNumber)?.Trim()
        };

        // Exposure Info
        int? iso = null;
        if (subIfd != null && subIfd.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var isoVal))
            iso = isoVal;

        double? expTime = null;
        if (subIfd != null && subIfd.TryGetDouble(ExifDirectoryBase.TagExposureTime, out var expVal))
            expTime = expVal;

        double? fNumber = null;
        if (subIfd != null && subIfd.TryGetDouble(ExifDirectoryBase.TagFNumber, out var fnVal))
            fNumber = fnVal;

        double? focalLength = null;
        if (subIfd != null && subIfd.TryGetDouble(ExifDirectoryBase.TagFocalLength, out var flVal))
            focalLength = flVal;

        double? focalLength35 = null;
        if (subIfd != null && (subIfd.TryGetDouble(0xA405, out var fl35Val) || subIfd.TryGetDouble(ExifDirectoryBase.TagFocalLength, out fl35Val)))
            focalLength35 = fl35Val;

        int? orientation = null;
        if (ifd0 != null && ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientVal))
            orientation = orientVal;

        var exposure = new ExposureInfo
        {
            Iso = iso,
            ExposureTimeSeconds = expTime,
            FNumber = fNumber,
            FocalLengthMm = focalLength,
            FocalLengthIn35MmFilm = focalLength35,
            ExposureProgram = subIfd?.GetDescription(ExifDirectoryBase.TagExposureProgram),
            MeteringMode = subIfd?.GetDescription(ExifDirectoryBase.TagMeteringMode),
            Flash = subIfd?.GetDescription(ExifDirectoryBase.TagFlash),
            WhiteBalance = subIfd?.GetDescription(ExifDirectoryBase.TagWhiteBalance),
            ColorSpace = subIfd?.GetDescription(ExifDirectoryBase.TagColorSpace)
        };

        // MakerNotes raw bytes
        byte[]? rawMakerNotes = null;
        if (subIfd != null && subIfd.ContainsTag(ExifDirectoryBase.TagMakernote))
        {
            rawMakerNotes = subIfd.GetByteArray(ExifDirectoryBase.TagMakernote);
        }

        return new ExifData
        {
            DateTimeOriginal = dtOriginal,
            CreateDate = dtCreate,
            ModifyDate = dtModify,
            OffsetTimeOriginal = subIfd?.GetString(0x9011),
            SubSecTimeOriginal = subIfd?.GetString(0x9291),
            Orientation = orientation,
            Camera = camera,
            Exposure = exposure,
            UserComment = subIfd?.GetDescription(ExifDirectoryBase.TagUserComment),
            ImageDescription = ifd0?.GetString(ExifDirectoryBase.TagImageDescription),
            Copyright = ifd0?.GetString(ExifDirectoryBase.TagCopyright),
            Artist = ifd0?.GetString(ExifDirectoryBase.TagArtist),
            RawMakerNotes = rawMakerNotes,
            AdditionalTags = rawTags
        };
    }

    private static GpsCoordinate? ExtractGps(IReadOnlyList<Directory> directories)
    {
        var gpsDir = directories.OfType<GpsDirectory>().FirstOrDefault();
        if (gpsDir == null)
            return null;

        var location = gpsDir.GetGeoLocation();
        if (location == null || location.IsZero)
            return null;

        double? altitude = null;
        if (gpsDir.TryGetDouble(GpsDirectory.TagAltitude, out var alt))
        {
            if (gpsDir.TryGetInt16(GpsDirectory.TagAltitudeRef, out var altRef) && altRef == 1)
                alt = -alt;
            altitude = alt;
        }

        double? direction = null;
        if (gpsDir.TryGetDouble(GpsDirectory.TagImgDirection, out var dir))
            direction = dir;

        double? speed = null;
        if (gpsDir.TryGetDouble(GpsDirectory.TagSpeed, out var spd))
            speed = spd;

        double? dop = null;
        if (gpsDir.TryGetDouble(GpsDirectory.TagDop, out var dopVal))
            dop = dopVal;

        DateTime? gpsTime = null;
        var dateStamp = gpsDir.GetString(GpsDirectory.TagDateStamp);
        var timeStamp = gpsDir.GetString(GpsDirectory.TagTimeStamp);
        if (!string.IsNullOrWhiteSpace(dateStamp) && !string.IsNullOrWhiteSpace(timeStamp))
        {
            if (DateTime.TryParse($"{dateStamp} {timeStamp}", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedGpsTime))
                gpsTime = parsedGpsTime.ToUniversalTime();
        }

        return new GpsCoordinate
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            AltitudeMeters = altitude,
            DirectionDegrees = direction,
            SpeedKmH = speed,
            DilutionOfPrecision = dop,
            ProcessingMethod = gpsDir.GetString(GpsDirectory.TagProcessingMethod),
            TimestampUtc = gpsTime
        };
    }

    private static IptcData ExtractIptc(IReadOnlyList<Directory> directories)
    {
        var iptcDir = directories.OfType<IptcDirectory>().FirstOrDefault();
        if (iptcDir == null)
            return new IptcData();

        var keywords = iptcDir.GetStringArray(IptcDirectory.TagKeywords)?.ToList() ?? new List<string>();

        DateTime? dateCreated = null;
        if (iptcDir.TryGetDateTime(IptcDirectory.TagDateCreated, out var dt))
            dateCreated = dt;

        return new IptcData
        {
            Title = iptcDir.GetString(IptcDirectory.TagHeadline),
            Caption = iptcDir.GetString(IptcDirectory.TagCaption),
            Byline = iptcDir.GetString(IptcDirectory.TagByLine),
            BylineTitle = iptcDir.GetString(IptcDirectory.TagByLineTitle),
            CopyrightNotice = iptcDir.GetString(IptcDirectory.TagCopyrightNotice),
            Credit = iptcDir.GetString(IptcDirectory.TagCredit),
            Source = iptcDir.GetString(IptcDirectory.TagSource),
            ObjectName = iptcDir.GetString(IptcDirectory.TagObjectName),
            City = iptcDir.GetString(IptcDirectory.TagCity),
            ProvinceState = iptcDir.GetString(IptcDirectory.TagProvinceOrState),
            Country = iptcDir.GetString(IptcDirectory.TagCountryOrPrimaryLocationName),
            CountryCode = iptcDir.GetString(IptcDirectory.TagCountryOrPrimaryLocationCode),
            DateCreated = dateCreated,
            Keywords = keywords
        };
    }

    private static XmpData ExtractXmp(IReadOnlyList<Directory> directories)
    {
        var xmpDir = directories.OfType<XmpDirectory>().FirstOrDefault();
        if (xmpDir == null)
            return new XmpData();

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keywords = new List<string>();
        string? desc = null;
        string? creator = null;
        string? rights = null;

        var xmpMeta = xmpDir.XmpMeta;
        if (xmpMeta != null)
        {
            foreach (var prop in xmpMeta.Properties)
            {
                if (!string.IsNullOrEmpty(prop.Path) && prop.Value != null)
                {
                    props[prop.Path] = prop.Value;
                    if (prop.Path.EndsWith("dc:subject", StringComparison.OrdinalIgnoreCase) ||
                        prop.Path.Contains("subject["))
                    {
                        keywords.Add(prop.Value);
                    }
                    else if (prop.Path.EndsWith("dc:description", StringComparison.OrdinalIgnoreCase))
                    {
                        desc = prop.Value;
                    }
                    else if (prop.Path.EndsWith("dc:creator", StringComparison.OrdinalIgnoreCase))
                    {
                        creator = prop.Value;
                    }
                    else if (prop.Path.EndsWith("dc:rights", StringComparison.OrdinalIgnoreCase))
                    {
                        rights = prop.Value;
                    }
                }
            }
        }

        return new XmpData
        {
            Properties = props,
            SubjectKeywords = keywords.Distinct().ToList(),
            Description = desc,
            Creator = creator,
            Rights = rights
        };
    }

    private static IccProfileData? ExtractIcc(IReadOnlyList<Directory> directories)
    {
        var iccDir = directories.OfType<IccDirectory>().FirstOrDefault();
        if (iccDir == null)
            return null;

        var profileDesc = iccDir.Tags.FirstOrDefault(t => t.Name.IndexOf("Description", StringComparison.OrdinalIgnoreCase) >= 0)?.Description;
        return new IccProfileData
        {
            ProfileName = profileDesc ?? iccDir.Name,
            ColorSpace = iccDir.GetString(IccDirectory.TagColorSpace)
        };
    }

    private static MigrationMarker? ExtractMarker(
        IReadOnlyList<Directory> directories,
        XmpData xmp,
        ExifData exif)
    {
        // 1. Look in all extracted directory tags
        foreach (var dir in directories)
        {
            foreach (var tag in dir.Tags)
            {
                var desc = tag.Description;
                if (!string.IsNullOrEmpty(desc))
                {
                    int idx = desc.IndexOf(MigrationMarker.MarkerPrefix, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var markerSub = desc.Substring(idx);
                        if (MigrationMarker.TryParse(markerSub, out var m))
                            return m;
                    }
                }
            }
        }

        // 2. Look in Xmp properties
        foreach (var kvp in xmp.Properties)
        {
            if (kvp.Key.IndexOf("PhotoForgeMigration", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (kvp.Value != null && MigrationMarker.TryParse(kvp.Value, out var m))
                    return m;
            }
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                int idx = kvp.Value.IndexOf(MigrationMarker.MarkerPrefix, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && MigrationMarker.TryParse(kvp.Value.Substring(idx), out var m))
                    return m;
            }
        }

        return null;
    }
}
