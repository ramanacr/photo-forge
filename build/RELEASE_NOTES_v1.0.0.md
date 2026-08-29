# PhotoForge v1.0.0 — Official Release

PhotoForge is an offline-first photo metadata continuity and modern format-conversion platform for Windows and Android.

---

## 🌟 What's New in v1.0.0

### 🛡️ Non-Destructive Provenance Restoration
- **Original Photo Immutability (`INV-01`):** Camera originals are opened strictly read-only and fingerprinted with SHA-256 before and after operations.
- **Deterministic Multi-Signal Candidate Matching:** High-speed candidate search utilizing filename tokens, capture timestamp proximity, aspect ratios, surviving camera EXIF remnants, perceptual visual hashing (64-bit dHash), and folder structures.
- **Strict Idempotency (`INV-02`):** Injects standard-compliant, namespaced migration markers (`pf:PhotoForgeMigration`) to eliminate duplicate re-processing.
- **Independent Verification Gate (`INV-03`):** Re-reads written files directly from disk to confirm EXIF, GPS, and format readability before atomic commit.

### 🔒 GPS Privacy Controls
- **Keep Exact:** Full 5-decimal coordinate preservation.
- **Remove:** Total stripping of GPS IFD, altitude, and location tags.
- **Round (1km):** Coordinates rounded to 2 decimal places to prevent exact domicile tracking.
- **CopyWithWarning:** Exact coordinates preserved with diff warning.

### 🎨 Native Desktop & Tools
- **Windows Desktop Application:** Modern WPF UI with Dark Fluent Theme, Quick Drag-and-Drop restore, batch processing studio, candidate match review table with manual override, and interactive metadata diff inspector.
- **Cross-Platform CLI (`photoforge`):** Commands for `restore`, `convert`, `verify`, `inspect`, `match`, `batch`, and `--json` scripting support.
- **Windows Explorer Integration:** Right-click context menu verbs for instant single-click restoration.
- **Android Platform Layer:** Zero-network manifest, Scoped Storage adapter, MediaStore bridge, and Share Sheet intent receivers.

---

## 📦 Release Artifacts & Checksums

| Package | Platform / Architecture | Description |
|---|---|---|
| `PhotoForge-v1.0.0-Windows-x64.zip` | Windows x64 | Standalone native WPF Desktop Application |
| `PhotoForge-v1.0.0-Windows-arm64.zip` | Windows ARM64 | Standalone native WPF Desktop Application |
| `PhotoForge-v1.0.0-CLI-win-x64.zip` | Windows x64 | Cross-platform standalone CLI tool (`photoforge.exe`) |
| `PhotoForge-v1.0.0-Android.zip` | Android | Android platform binaries and integration package |
| `sbom.spdx.json` | Any | SPDX 2.3 Software Bill of Materials |
| `SHA256SUMS.txt` | Any | SHA-256 cryptographic checksums for all packages |

---

## 🔒 Security & Privacy Notice
PhotoForge operates **100% offline**. Zero network requests, analytics, or telemetry are ever initiated.
