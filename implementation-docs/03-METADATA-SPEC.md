# PhotoForge Metadata Specification

## 1. Principle

The original photo is the **metadata authority** for metadata that describes capture/provenance.

The edited photo remains authoritative for metadata that intentionally describes the editing/export state or user edits, unless a user profile explicitly overrides that policy.

## 2. Metadata classes

| Class | Default source | Notes |
|---|---|---|
| Capture date/time | Original | Preserve original capture semantics |
| GPS | Original | Strong preservation target; user privacy policy can remove |
| Camera make/model | Original | Preserve |
| Lens | Original | Preserve |
| Exposure | Original | Preserve |
| Orientation | Target / derived | Must be validated against actual pixels |
| Software | Target | Editing application metadata usually belongs to target |
| Copyright | Original + target conflict policy | Configurable |
| Artist/Author | Original + target conflict policy | Configurable |
| IPTC | Merge | Field-specific semantics |
| XMP | Merge | Namespace-aware |
| ICC profile | Target/image pipeline | Preserve valid color interpretation |
| MakerNotes | Original where byte-safe | Treat as opaque where necessary |
| Thumbnail | Regenerate/skip | Never blindly copy if dimensions/rotation differ |
| Editing history | Target | Preserve if valid |
| User description | Target unless missing | Avoid deleting intentional edits |

## 3. Conflict policy

Default is **B** from discovery: preserve target metadata and add missing original metadata.

Refined rule:
1. Fields classified as capture/provenance -> original wins.
2. Fields classified as edit-state -> target wins.
3. Fields classified as user-content -> merge/preserve target where meaningful.
4. Unknown fields -> preserve target; add original only if non-conflicting and supported.
5. Profile may override field-level policy.

## 4. GPS

Default:
- copy latitude
- copy longitude
- copy altitude
- copy GPS timestamp where valid
- copy direction where valid
- copy GPS processing metadata where representable

Privacy modes:
- `KEEP_EXACT`
- `REMOVE`
- `ROUND`
- `COPY_WITH_WARNING`

Never silently expose GPS in a newly created share/export if user selected removal.

## 5. EXIF

Support normalized handling of:
- `DateTimeOriginal`
- `CreateDate`
- `ModifyDate`
- `Make`
- `Model`
- `LensMake`
- `LensModel`
- exposure fields
- focal length
- orientation
- ISO
- flash
- metering
- white balance
- image dimensions
- GPS block
- supported MakerNotes

MakerNotes must be handled conservatively. Invalid pointer offsets or relocated vendor data can make a raw block unsafe to copy after re-encoding.

## 6. XMP

Use namespace-aware merge rules.

Keep PhotoForge fields in an application-owned namespace, conceptually:

```text
http://photoforge.example/ns/1.0/
```

Final production namespace must be chosen after branding/domain ownership.

Do not invent public namespace URIs before the brand is finalized.

## 7. IPTC

Prefer standardized field mapping where supported. Track:
- keywords
- caption/description
- creator
- rights
- location
- date
- contact

Merge must not create duplicate keywords unnecessarily.

## 8. ICC

The actual pixels and ICC profile must remain semantically consistent.

When pixels are re-encoded:
- carry forward the applicable profile,
- validate that the output declares a compatible color space,
- report when a source profile could not be preserved.

## 9. Metadata diff model

Every operation should support a structured diff:

```json
{
  "copied": ["GPSLatitude", "GPSLongitude", "DateTimeOriginal"],
  "preserved_target": ["Software", "XMP-dc:Description"],
  "overwritten": [],
  "skipped": ["MakerNotes"],
  "failed": [],
  "warnings": ["Target is rotated; thumbnail was regenerated"]
}
```

## 10. Migration marker

Required conceptual fields:
- `processed=true`
- `sourceFingerprint`
- `profile`
- `migrationVersion`
- `engineVersion`

The fingerprint should identify the source that justified the migration, not merely the target filename.

## 11. Fingerprint

Use layered identity:
- strong file fingerprint where feasible,
- metadata-derived fingerprint,
- perceptual image fingerprint for matching.

Do not hash only filenames.

## 12. Atomicity

Never directly overwrite a valid target without a recoverable temporary output path.

Recommended:
`target -> temp write -> validate -> atomic replace`.

## 13. Information preservation rule

When a metadata field cannot be represented in the destination:
- retain it in audit output,
- report it,
- never falsely mark it as preserved.

## 14. Future versioning

`MetadataProfile` is a versioned contract. Never mutate the semantics of `standard-v1` silently.
Create `standard-v2` when behavior changes materially.
