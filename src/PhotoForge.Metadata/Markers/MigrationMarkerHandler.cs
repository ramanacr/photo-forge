using System;
using System.Text;
using PhotoForge.Core.Models;

namespace PhotoForge.Metadata.Markers;

/// <summary>
/// Helper for reading, writing, and validating PhotoForge migration markers.
/// </summary>
public static class MigrationMarkerHandler
{
    public const string XmpNamespace = "http://photoforge.example/ns/1.0/";
    public const string XmpPrefix = "pf";
    public const string XmpPropertyName = "pf:PhotoForgeMigration";

    /// <summary>
    /// Creates a MigrationMarker for a given source photo fingerprint and profile.
    /// </summary>
    public static MigrationMarker CreateMarker(string sourceFingerprint, string profileName = "standard-v1")
    {
        return new MigrationMarker
        {
            Processed = true,
            SourceFingerprint = sourceFingerprint,
            Profile = profileName,
            MigrationVersion = MigrationMarker.CurrentSchemaVersion,
            EngineVersion = "1.0.0",
            ProcessedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Checks whether an existing target metadata document contains a valid marker matching the source fingerprint and profile.
    /// </summary>
    public static bool IsAlreadyMigrated(MetadataDocument targetMetadata, string sourceFingerprint, string profileName = "standard-v1")
    {
        if (targetMetadata.Marker == null)
            return false;

        var marker = targetMetadata.Marker;
        if (!marker.Processed)
            return false;

        // Check if the migration was performed from the same source photo fingerprint
        return string.Equals(marker.SourceFingerprint, sourceFingerprint, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(marker.Profile, profileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formats the migration marker as an XML snippet for XMP embedding.
    /// </summary>
    public static string ToXmpXml(MigrationMarker marker)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<pf:PhotoForgeMigration xmlns:pf=\"{XmpNamespace}\"");
        sb.AppendLine($"    pf:processed=\"{(marker.Processed ? "true" : "false")}\"");
        sb.AppendLine($"    pf:sourceFingerprint=\"{marker.SourceFingerprint}\"");
        sb.AppendLine($"    pf:profile=\"{marker.Profile}\"");
        sb.AppendLine($"    pf:migrationVersion=\"{marker.MigrationVersion}\"");
        sb.AppendLine($"    pf:engineVersion=\"{marker.EngineVersion}\"");
        sb.AppendLine($"    pf:processedAt=\"{marker.ProcessedAtUtc:O}\" />");
        return sb.ToString();
    }
}
