# PhotoForge Format Compatibility Matrix

> This is the target capability matrix. Actual support must be proven by fixtures and integration tests before a format is marketed as supported.

Legend:
- **R** = restore/metadata-only operation targeted
- **C** = conversion to HEIC targeted
- **V** = verify/inspect
- **P** = pixel re-encoding is required for the operation
- **L** = limited / field-specific behavior
- **N** = not a v1 promise

| Format | Read | Metadata restore | HEIC output | Notes |
|---|---:|---:|---:|---|
| JPEG/JPG | Yes | Yes | Yes | Best v1 path |
| PNG | Yes | Yes | Yes | Metadata semantics differ from EXIF-centric JPEG |
| WebP | Yes | Yes | Yes | Verify XMP/EXIF mappings |
| TIFF | Yes | Yes | Yes | Large files possible |
| BMP | Yes | Limited | Yes | Little native metadata |
| GIF | Yes | Limited | Yes | Animated GIF policy needed |
| AVIF | Yes | Yes | Yes | Container/metadata nuances |
| HEIC | Yes | Yes | N/A | HEIC -> HEIC metadata pipeline |
| HEIF | Yes | Yes | N/A | HEIF family container |
| DNG | Yes | Metadata-focused | Yes | No RAW pixel development in v1 |
| CR2 | Future | Future | Future | RAW pixel support deferred |
| CR3 | Future | Future | Future | RAW support deferred |
| NEF | Future | Future | Future | RAW support deferred |
| ARW | Future | Future | Future | RAW support deferred |
| RAF | Future | Future | Future | RAW support deferred |
| ORF | Future | Future | Future | RAW support deferred |
| RW2 | Future | Future | Future | RAW support deferred |

## HEIC notes

libheif currently documents support for HEIC, AVIF and additional codecs, as well as EXIF/XMP metadata reading and auxiliary images. Source: https://github.com/strukturag/libheif

Windows WIC exposes a HEIF codec extension and compression options, but codec availability and platform configuration can vary. PhotoForge should avoid making core correctness depend on an end-user-installed codec. Source: https://learn.microsoft.com/en-us/windows/win32/wic/heif-codec

Android exposes HEIF-related capabilities through its media stack; use Android APIs as platform adapters, not as the shared metadata model. Source: https://developer.android.com/reference/android/hardware/DataSpace

## Conversion rules

### Metadata-only operation
Where possible, modify the existing container without decoding/re-encoding pixels.

### Conversion operation
When changing format:
1. decode/source pixel interpretation
2. capture source metadata
3. encode destination
4. inject compatible metadata
5. validate
6. report any lossy metadata mapping

## Quality modes
- `LOSSLESS_WHERE_SUPPORTED`
- `VERY_HIGH`
- `HIGH`
- `BALANCED`
- `SMALL`
- `CUSTOM`

The implementation must document which codec quality knobs correspond to each logical mode.

## RAW policy

Do not claim full RAW support until:
- decoding behavior is validated,
- vendor MakerNotes are protected,
- source color management is correct,
- test coverage includes camera-specific fixtures.
