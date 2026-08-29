# PhotoForge Architecture & Technical Specification

PhotoForge is an offline-first photo metadata continuity and format-conversion platform for Windows and Android.

## System Topology

```
                         PhotoForge
                             |
           +------------------+------------------+
           |                                     |
    PhotoForge Core                       Platform Layer
           |                       +-------------+-------------+
           |                       |                           |
    +------+------+                Windows                   Android
    |             |                 |                           |
Metadata     Matching       GUI / Explorer / CLI      UI / Share / MediaStore
    |             |              FileSystem              DocumentProvider
    +------+------+ 
           |
       Imaging
           |
    Decode / Encode
           |
    +------+------+---------+
    |      |      |         |
  JPEG   PNG    WebP   HEIF/AVIF
```

## Module Breakdown

1. **`PhotoForge.Core`**:
   - `PhotoFormat`, `PhotoRef`, `MetadataDocument`, `GpsData`, `CameraInfo`, `ExposureInfo`, `IptcData`, `XmpData`.
   - `MigrationMarker`, `MetadataDiff`, `OperationResult`, `VerificationResult`, `MergeProfile`.
   - Pipeline Orchestrator (`PhotoForgePipeline`).
   - Typed error hierarchy and invariants (`INV-01` to `INV-06`).

2. **`PhotoForge.Metadata`**:
   - Resilient EXIF, GPS, IPTC, XMP, ICC parsing and normalization.
   - Conflict resolution engine:
     - Capture/provenance data -> Original wins.
     - Edit-state data -> Target wins.
     - Keywords & XMP tags -> Smart union merge.
     - GPS Privacy Modes -> `KeepExact`, `Remove`, `Round`, `CopyWithWarning`.
   - Namespaced migration marker injection (`pf:PhotoForgeMigration`).

3. **`PhotoForge.Matching`**:
   - Multi-signal deterministic scoring engine:
     - `FilenameSignal` (0.20): Levenshtein distance, suffix stripping (`_edit`, `-copy`, `-final`).
     - `TimestampSignal` (0.15): Proximity of `DateTimeOriginal` with exponential decay.
     - `DimensionsSignal` (0.10): Aspect ratio and dimension scale comparison.
     - `MetadataRemnantsSignal` (0.10): Surviving camera make/model and lens clues.
     - `PerceptualSignal` (0.35): Difference Hash (dHash) Hamming distance.
     - `DirectorySignal` (0.10): Directory hierarchy and folder pairing relationships.
   - Staged filtering for scaling up to 100,000+ photo collections.
   - Decision confidence bands (`AutoAccept`, `Suggested`, `UserReviewRequired`, `NoMatch`).

4. **`PhotoForge.Imaging`**:
   - Format detection via magic byte sniffing.
   - Bounds-only fast inspection (`Image.Identify`).
   - Perceptual hash generation (`dHash`).
   - HEIC / WebP conversion pipeline with metadata continuity.

5. **`PhotoForge.Storage` & `PhotoForge.Audit`**:
   - Read-only source opening and SHA-256 pre/post immutability assertion.
   - Safe unique temp file writes (`<target>.tmp.photoforge.<guid>`).
   - Independent verification before atomic commit.
   - Local SQLite operational audit database (`photoforge_history.db`).
   - JSON, Markdown, and CSV audit report exporter.

6. **Applications & Platforms**:
   - `PhotoForge.Cli`: Full-featured cross-platform command-line tool.
   - `PhotoForge.Desktop`: Modern WPF Windows 11 GUI with Fluent theme.
   - `PhotoForge.Shell`: Windows Explorer Context Menu integration.
   - `PhotoForge.Android`: Android platform adapter with Scoped Storage and Share Intent support.
