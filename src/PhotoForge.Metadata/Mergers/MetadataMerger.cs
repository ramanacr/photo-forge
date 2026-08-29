using System;
using System.Collections.Generic;
using System.Linq;
using PhotoForge.Core.Models;
using PhotoForge.Metadata.Markers;

namespace PhotoForge.Metadata.Mergers;

/// <summary>
/// Deterministic conflict resolution and metadata merge engine.
/// </summary>
public class MetadataMerger
{
    public MetadataDocument Merge(
        MetadataDocument original,
        MetadataDocument target,
        string sourceFingerprint,
        MergeProfile profile,
        out MetadataDiff diff)
    {
        var copied = new List<string>();
        var preserved = new List<string>();
        var overwritten = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();
        var warnings = new List<string>();

        // 1. Resolve GPS based on privacy policy
        GpsCoordinate? resolvedGps = null;
        switch (profile.GpsPolicy)
        {
            case GpsPrivacyPolicy.KeepExact:
                if (original.Gps != null)
                {
                    resolvedGps = original.Gps;
                    copied.Add("GPS.Latitude");
                    copied.Add("GPS.Longitude");
                    if (original.Gps.AltitudeMeters.HasValue) copied.Add("GPS.Altitude");
                    if (original.Gps.TimestampUtc.HasValue) copied.Add("GPS.Timestamp");
                }
                else if (target.Gps != null)
                {
                    resolvedGps = target.Gps;
                    preserved.Add("GPS.TargetLocation");
                }
                break;

            case GpsPrivacyPolicy.Remove:
                resolvedGps = null;
                if (original.Gps != null || target.Gps != null)
                {
                    skipped.Add("GPS.StrippedDueToPrivacyPolicy");
                }
                break;

            case GpsPrivacyPolicy.Round:
                if (original.Gps != null)
                {
                    resolvedGps = new GpsCoordinate
                    {
                        Latitude = Math.Round(original.Gps.Latitude, 2),
                        Longitude = Math.Round(original.Gps.Longitude, 2),
                        AltitudeMeters = original.Gps.AltitudeMeters.HasValue ? Math.Round(original.Gps.AltitudeMeters.Value, 0) : null,
                        ProcessingMethod = "PhotoForge_PrivacyRounded",
                        TimestampUtc = original.Gps.TimestampUtc
                    };
                    copied.Add("GPS.Latitude(Rounded)");
                    copied.Add("GPS.Longitude(Rounded)");
                    warnings.Add("GPS coordinates were rounded to ~1km for privacy protection.");
                }
                break;

            case GpsPrivacyPolicy.CopyWithWarning:
                if (original.Gps != null)
                {
                    resolvedGps = original.Gps;
                    copied.Add("GPS.Latitude");
                    copied.Add("GPS.Longitude");
                    warnings.Add("Output contains exact GPS coordinates from the original photo.");
                }
                break;
        }

        // 2. Resolve EXIF capture/provenance fields (Original is authority)
        var origExif = original.Exif;
        var targetExif = target.Exif;

        DateTime? dtOriginal = origExif.DateTimeOriginal ?? targetExif.DateTimeOriginal;
        if (origExif.DateTimeOriginal.HasValue)
            copied.Add("EXIF.DateTimeOriginal");
        else if (targetExif.DateTimeOriginal.HasValue)
            preserved.Add("EXIF.DateTimeOriginal");

        DateTime? dtCreate = origExif.CreateDate ?? targetExif.CreateDate;
        if (origExif.CreateDate.HasValue)
            copied.Add("EXIF.CreateDate");

        DateTime? dtModify = profile.PreferTargetForEditState
            ? (targetExif.ModifyDate ?? DateTime.UtcNow)
            : (origExif.ModifyDate ?? targetExif.ModifyDate);
        if (profile.PreferTargetForEditState)
            preserved.Add("EXIF.ModifyDate(Target)");

        // Camera make & model
        var origCam = origExif.Camera;
        var targetCam = targetExif.Camera;
        var resolvedCam = new CameraInfo
        {
            Make = origCam.Make ?? targetCam.Make,
            Model = origCam.Model ?? targetCam.Model,
            SerialNumber = origCam.SerialNumber ?? targetCam.SerialNumber,
            LensMake = origCam.LensMake ?? targetCam.LensMake,
            LensModel = origCam.LensModel ?? targetCam.LensModel,
            LensSerialNumber = origCam.LensSerialNumber ?? targetCam.LensSerialNumber,
            Software = profile.PreferTargetForEditState ? (targetCam.Software ?? origCam.Software) : (origCam.Software ?? targetCam.Software),
            HostComputer = targetCam.HostComputer ?? origCam.HostComputer
        };

        if (!string.IsNullOrEmpty(origCam.Make)) copied.Add("Camera.Make");
        if (!string.IsNullOrEmpty(origCam.Model)) copied.Add("Camera.Model");
        if (!string.IsNullOrEmpty(origCam.LensModel)) copied.Add("Camera.LensModel");
        if (!string.IsNullOrEmpty(targetCam.Software)) preserved.Add("Software(TargetEditor)");

        // Exposure info
        var origExp = origExif.Exposure;
        var targetExp = targetExif.Exposure;
        var resolvedExp = new ExposureInfo
        {
            Iso = origExp.Iso ?? targetExp.Iso,
            ExposureTimeSeconds = origExp.ExposureTimeSeconds ?? targetExp.ExposureTimeSeconds,
            FNumber = origExp.FNumber ?? targetExp.FNumber,
            FocalLengthMm = origExp.FocalLengthMm ?? targetExp.FocalLengthMm,
            FocalLengthIn35MmFilm = origExp.FocalLengthIn35MmFilm ?? targetExp.FocalLengthIn35MmFilm,
            ExposureProgram = origExp.ExposureProgram ?? targetExp.ExposureProgram,
            MeteringMode = origExp.MeteringMode ?? targetExp.MeteringMode,
            Flash = origExp.Flash ?? targetExp.Flash,
            WhiteBalance = origExp.WhiteBalance ?? targetExp.WhiteBalance,
            ExposureBiasValue = origExp.ExposureBiasValue ?? targetExp.ExposureBiasValue,
            ColorSpace = targetExp.ColorSpace ?? origExp.ColorSpace
        };

        if (origExp.Iso.HasValue) copied.Add("Exposure.ISO");
        if (origExp.ExposureTimeSeconds.HasValue) copied.Add("Exposure.ExposureTime");
        if (origExp.FNumber.HasValue) copied.Add("Exposure.FNumber");
        if (origExp.FocalLengthMm.HasValue) copied.Add("Exposure.FocalLength");

        // Orientation (target usually determines actual rendering)
        int? resolvedOrientation = targetExif.Orientation ?? origExif.Orientation;
        if (targetExif.Orientation.HasValue)
            preserved.Add("EXIF.Orientation(Target)");
        else if (origExif.Orientation.HasValue)
            copied.Add("EXIF.Orientation");

        // MakerNotes handling
        byte[]? resolvedMakerNotes = null;
        if (profile.CopyMakerNotesIfSafe && origExif.RawMakerNotes != null && origExif.RawMakerNotes.Length > 0)
        {
            resolvedMakerNotes = origExif.RawMakerNotes;
            copied.Add("EXIF.MakerNotes(ByteSafe)");
        }
        else if (origExif.RawMakerNotes != null)
        {
            skipped.Add("EXIF.MakerNotes(SkippedByProfile)");
        }

        // Descriptions & Comments
        string? userComment = targetExif.UserComment ?? origExif.UserComment;
        string? imageDesc = targetExif.ImageDescription ?? origExif.ImageDescription;
        string? copyright = origExif.Copyright ?? targetExif.Copyright;
        string? artist = origExif.Artist ?? targetExif.Artist;

        var mergedExif = new ExifData
        {
            DateTimeOriginal = dtOriginal,
            CreateDate = dtCreate,
            ModifyDate = dtModify,
            OffsetTimeOriginal = origExif.OffsetTimeOriginal ?? targetExif.OffsetTimeOriginal,
            SubSecTimeOriginal = origExif.SubSecTimeOriginal ?? targetExif.SubSecTimeOriginal,
            Orientation = resolvedOrientation,
            Camera = resolvedCam,
            Exposure = resolvedExp,
            UserComment = userComment,
            ImageDescription = imageDesc,
            Copyright = copyright,
            Artist = artist,
            RawMakerNotes = resolvedMakerNotes
        };

        // 3. Resolve IPTC (Union merge keywords, retain target caption if edited)
        var origIptc = original.Iptc;
        var targetIptc = target.Iptc;

        var combinedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kw in origIptc.Keywords) combinedKeywords.Add(kw);
        if (profile.PreserveTargetKeywords)
        {
            foreach (var kw in targetIptc.Keywords) combinedKeywords.Add(kw);
        }

        if (origIptc.Keywords.Count > 0) copied.Add($"IPTC.Keywords({origIptc.Keywords.Count} tags)");
        if (targetIptc.Keywords.Count > 0) preserved.Add($"IPTC.Keywords({targetIptc.Keywords.Count} tags)");

        var mergedIptc = new IptcData
        {
            Title = targetIptc.Title ?? origIptc.Title,
            Caption = targetIptc.Caption ?? origIptc.Caption,
            Byline = origIptc.Byline ?? targetIptc.Byline,
            BylineTitle = origIptc.BylineTitle ?? targetIptc.BylineTitle,
            CopyrightNotice = origIptc.CopyrightNotice ?? targetIptc.CopyrightNotice,
            Credit = origIptc.Credit ?? targetIptc.Credit,
            Source = origIptc.Source ?? targetIptc.Source,
            ObjectName = origIptc.ObjectName ?? targetIptc.ObjectName,
            City = origIptc.City ?? targetIptc.City,
            ProvinceState = origIptc.ProvinceState ?? targetIptc.ProvinceState,
            Country = origIptc.Country ?? targetIptc.Country,
            CountryCode = origIptc.CountryCode ?? targetIptc.CountryCode,
            DateCreated = origIptc.DateCreated ?? targetIptc.DateCreated,
            Keywords = combinedKeywords.ToList()
        };

        // 4. Resolve XMP
        var origXmp = original.Xmp;
        var targetXmp = target.Xmp;
        var mergedXmpProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Copy original XMP properties
        foreach (var kvp in origXmp.Properties)
            mergedXmpProps[kvp.Key] = kvp.Value;

        // Preserve target-specific XMP properties
        foreach (var kvp in targetXmp.Properties)
            mergedXmpProps[kvp.Key] = kvp.Value;

        var xmpKeywords = new HashSet<string>(origXmp.SubjectKeywords, StringComparer.OrdinalIgnoreCase);
        foreach (var kw in targetXmp.SubjectKeywords) xmpKeywords.Add(kw);

        var mergedXmp = new XmpData
        {
            Properties = mergedXmpProps,
            SubjectKeywords = xmpKeywords.ToList(),
            Description = targetXmp.Description ?? origXmp.Description,
            Creator = origXmp.Creator ?? targetXmp.Creator,
            Rights = origXmp.Rights ?? targetXmp.Rights
        };

        // 5. ICC Profile
        var resolvedIcc = target.IccProfile ?? original.IccProfile;
        if (target.IccProfile != null)
            preserved.Add("ICCProfile(Target)");
        else if (original.IccProfile != null)
            copied.Add("ICCProfile(Original)");

        // 6. Generate Migration Marker
        var marker = MigrationMarkerHandler.CreateMarker(sourceFingerprint, profile.Name);
        copied.Add("PhotoForge.MigrationMarker");

        diff = new MetadataDiff
        {
            CopiedFromOriginal = copied,
            PreservedFromTarget = preserved,
            Overwritten = overwritten,
            Skipped = skipped,
            Failed = failed,
            Warnings = warnings
        };

        return new MetadataDocument
        {
            Exif = mergedExif,
            Gps = resolvedGps,
            Iptc = mergedIptc,
            Xmp = mergedXmp,
            IccProfile = resolvedIcc,
            Marker = marker
        };
    }
}
