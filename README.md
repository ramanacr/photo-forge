<p align="center">
  <img src="docs/branding/photoforge_banner.jpg" alt="PhotoForge Hero Banner" width="100%" />
</p>

<p align="center">
  <img src="docs/branding/photoforge_logo.jpg" alt="PhotoForge Logo" width="120" />
</p>

<p align="center">
  <strong>Offline-first Photo Metadata Continuity &amp; Modern Format Conversion Suite for Windows &amp; Android</strong>
</p>

<p align="center">
  <em>Edit freely. Keep everything.</em>
</p>

<p align="center">
  <a href="https://github.com/ramanacr/photo-forge/releases"><img src="https://img.shields.io/github/v/release/ramanacr/photo-forge?style=flat-square&color=00B4D8" alt="GitHub Release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green.svg?style=flat-square" alt="License" /></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/9.0"><img src="https://img.shields.io/badge/.NET-9.0-purple.svg?style=flat-square" alt=".NET 9.0" /></a>
  <a href="https://developer.android.com/"><img src="https://img.shields.io/badge/Android-APK-3DDC84.svg?style=flat-square&logo=android&logoColor=white" alt="Android APK" /></a>
</p>

---

## 🌟 Overview

When photos are edited in popular software (Lightroom, Photoshop, Snapseed, mobile editors, social media tools) or saved as exports, crucial provenance and capture metadata is routinely stripped or degraded:
- Original camera make & model, lens specifications, and serial numbers
- Precise photographic exposure settings (shutter speed, aperture, ISO, focal length, flash)
- Original capture timestamp and subsecond timing
- GPS coordinates, altitude, and heading
- Original color profiles, IPTC keywords, and maker notes

**PhotoForge** solves photo metadata loss across desktop and mobile by linking edited photos back to their originals through deterministic multi-signal candidate matching and restoring capture provenance without ever touching or modifying original camera files.

---

## 🚀 Key Features

### 💻 Desktop & CLI Suite (Windows x64 / ARM64)
- **🛡️ 100% Original Immutability (`INV-01`):** Camera originals are opened strictly read-only with cryptographic pre/post SHA-256 integrity verification.
- **⚡ Deterministic Multi-Signal Matching:** Combines filename edit distance, capture timestamps, aspect ratios, surviving EXIF remnants, perceptual visual hashing (64-bit dHash), and folder structures.
- **🔒 Granular Privacy Control for GPS:** Choose between `KeepExact`, `Remove`, `Round` (~1km privacy blur), or `CopyWithWarning`.
- **🔁 Strict Idempotency & Markers (`INV-02`):** Injects standard-compliant, namespaced migration markers (`PF-MIG`) to prevent redundant re-processing.
- **🔍 Independent Post-Write Verification (`INV-03`):** Every written file is independently re-read from disk to confirm EXIF, GPS, and image integrity before final commit.
- **💎 Modern Format Studio:** High-efficiency WebP, JPEG, and HEIC format transcoding with complete metadata continuity.
- **🖥️ Native Windows Fluent UI:** Modern WPF application with dark Fluent theme, interactive Diff Inspector, drag-and-drop restore, and batch management.
- **💻 Cross-Platform CLI Tool:** Full-featured command-line utility with rich terminal UI and machine-readable `--json` output.
- **🗂️ Windows File Explorer Integration:** Right-click context menu integration for instantaneous restoration directly from File Explorer.

### 📱 Native Android Application (APK)
- **⚡ Quick Metadata Restore:** Seamlessly select an edited photo and original camera photo to restore full provenance.
- **📁 Batch Album Studio:** Process an entire directory or selection of edited photos against your camera roll with live progress and comprehensive reporting.
- **🔄 Format Conversion Studio:** Transcode images to WebP (Lossless/Lossy), JPEG, or PNG with quality presets while preserving all metadata.
- **🔍 Metadata Inspector:** Deep inspection of EXIF (Camera, Lens, ISO, F-number, Shutter), GPS Location & Elevation, IPTC Keywords, and PhotoForge Provenance markers.
- **🛡️ Continuity & Integrity Verifier:** Standalone verifier testing image stream decodability, pixel dimension validity, EXIF tag preservation, and migration markers.
- **🎯 Candidate Match Finder:** Interactive candidate ranking tool visualizing confidence bands (Auto-Accept, Suggested, Review Required) and signal breakdowns.
- **🔍 Metadata Diff Inspector:** Side-by-side categorized tag diff viewer (Copied from Original, Preserved from Target, Warnings).
- **⚙️ Persistent Preferences:** Configure GPS privacy policies, default formats, quality presets, and auto-accept thresholds.
- **📲 Android Share Sheet Integration:** Send images directly from Google Photos or your Gallery app to PhotoForge for 1-tap restore.
- **🔒 100% Offline & Local:** Zero internet permissions, zero cloud dependencies, zero telemetry.

---

## 🏛️ Architecture & System Design

```
                                 PhotoForge Solution
                                          │
            ┌─────────────────────────────┴─────────────────────────────┐
            ▼                                                           ▼
     PhotoForge Core                                             Platform Layer
  (Models, Invariants, Pipeline)                               (Windows, Android)
            │                                                           │
   ┌────────┴────────┬───────────────────┐                              │
   ▼                 ▼                   ▼                              │
Metadata          Matching            Imaging                           │
(EXIF/GPS/XMP)  (dHash/Signals)    (Decode/Encode)                      │
   │                 │                   │                              │
   └────────┬────────┴───────────────────┘                              │
            ▼                                                           ▼
     Storage & Audit                                             Applications
(Atomic IO, SQLite, Reports)                               (WPF Desktop, CLI, Android)
```

| Component | Description |
|---|---|
| `PhotoForge.Core` | Core domain models, interfaces, typed error hierarchy, pipeline orchestrator, and invariant guarantees |
| `PhotoForge.Metadata` | EXIF (SubIFD, IFD0), GPS, IPTC, XMP parser, metadata merger with conflict resolution, and migration marker handler |
| `PhotoForge.Imaging` | Magic byte format sniffer, bounds inspector, 64-bit dHash perceptual hasher, and format converter |
| `PhotoForge.Matching` | Multi-signal scoring engine (`Filename`, `Timestamp`, `Dimensions`, `MetadataRemnants`, `Perceptual`, `Directory`) |
| `PhotoForge.Storage` | Cryptographic SHA-256 fingerprinting, atomic safe file replacement, and SQLite operational audit repository |
| `PhotoForge.Audit` | JSON, Markdown, and CSV audit report generation |
| `PhotoForge.Platform` | Cross-platform abstractions and OS-specific bridges |
| `PhotoForge.Shell` | Windows Explorer context menu verb registration |
| `PhotoForge.Cli` | Spectre.Console-powered CLI tool (`photoforge`) |
| `PhotoForge.Desktop` | Modern WPF GUI application with Fluent dark theme and Diff Inspector |
| `PhotoForge.Android` | Native Kotlin/Gradle Android application with Material 3 UI and Scoped Storage bridge |

---

## 🧪 Critical Invariant Guarantees

PhotoForge enforces 6 critical system invariants validated by automated integration test suites:

1. **`INV-01: Original Immutability`** — Source photos are opened read-only. SHA-256 hashes before and after execution are guaranteed identical.
2. **`INV-02: Idempotency`** — Re-running on already migrated files returns `OperationStatus.Skipped` without modifying the target.
3. **`INV-03: Independent Verification`** — Every output file is re-read from disk to independently verify EXIF tags, GPS coords, and image readability.
4. **`INV-04: No Silent Loss`** — Unsupported or modified metadata tags are explicitly captured in diff records and warnings.
5. **`INV-05: Atomic Safety`** — Temporary staging files ensure cancelled or aborted runs leave zero corrupt files on disk.
6. **`INV-06: 100% Offline`** — Zero socket connections or network requests are ever made.

---

## 🛠️ CLI Quick Reference

```powershell
# Restore metadata from original to edited photo
photoforge restore --original "IMG_001.jpg" --edited "IMG_001_edited.jpg" --output "IMG_001_restored.jpg"

# Convert format with metadata continuity
photoforge convert --input "photo.jpg" --format webp --quality high

# Match candidates in a folder using multi-signal scoring
photoforge match --edited "IMG_001_edit.jpg" --originals "D:\Originals"

# Batch process an entire album
photoforge batch --input "D:\Edited" --originals "D:\Originals" --output "D:\Restored" --auto-accept

# Inspect all metadata categories and migration markers
photoforge inspect --input "restored.jpg" --json

# Independently verify file integrity & continuity
photoforge verify --input "restored.jpg"

# Register Windows Explorer right-click context menu
photoforge --register-shell
```

---

## 📦 Building & Releases

### Build All Releases
```powershell
./build/publish-release.ps1 -Bump minor
```

### Release Artifacts
- 📱 `PhotoForge-v{version}.apk` — Native Android Application (Signed & R8 Optimized)
- 🪟 `PhotoForge-Setup-v{version}-x64.exe` — Windows Native Installer
- 📦 `PhotoForge-v{version}-Windows-x64.zip` — Portable Windows x64 binaries
- 📦 `PhotoForge-v{version}-Windows-arm64.zip` — Portable Windows ARM64 binaries
- 💻 `PhotoForge-v{version}-CLI-win-x64.zip` — Cross-platform standalone CLI suite
- 📱 `PhotoForge-v{version}-Android.zip` — Android source + .NET bridge archive
- 🔐 `SHA256SUMS.txt` — SHA-256 verification checksums

---

## 📚 Documentation & Guides

- **[User Guide](docs/USER_GUIDE.md):** Complete guide for Desktop, Android, and CLI usage.
- **[CLI Reference](docs/CLI_REFERENCE.md):** Command-line syntax, options, and JSON schema reference.
- **[Store Publishing Guide](docs/STORE_PUBLISHING_GUIDE.md):** Google Play Store and Microsoft Store publishing instructions.
- **[Code Signing Guide](docs/SIGNING_GUIDE.md):** Authenticode code signing setup.
- **[Privacy Policy](docs/PRIVACY.md):** Zero-telemetry offline declaration.

---

## 📄 License

MIT License. See [LICENSE](LICENSE) for details.
