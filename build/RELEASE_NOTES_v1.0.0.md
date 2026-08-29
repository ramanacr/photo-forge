# PhotoForge v1.0.0 — Official Release

PhotoForge is an offline-first photo metadata continuity and modern format-conversion platform for Windows and Android.

---

## 🌟 What's New in v1.0.0

### 🖥️ Windows Native Setup Installer & Desktop Suite
- **Native Setup Installer (`PhotoForge-Setup-v1.0.0-x64.exe`):** Single-file standalone Windows installer with dark Fluent theme wizard, Start Menu & Desktop shortcut creation, PATH environment integration, Explorer context menu registration, and complete uninstaller support in Windows Settings / Control Panel.
- **WPF Desktop Application:** Modern Fluent dark UI with Quick Restore Drag & Drop, Batch Studio, Candidate Match Review table with manual override, Interactive Metadata Diff Inspector, and HEIC Studio.
- **Standalone CLI (`photoforge.exe`):** Scriptable cross-platform tool with `--json`, `restore`, `convert`, `verify`, `inspect`, `match`, and `batch`.
- **Inno Setup Script (`photoforge.iss`):** Compilable Inno Setup installer script for enterprise IT administrators.

### 📱 Android Application & Scoped Storage Engine
- **Android Application Suite (`com.photoforge.app`):**
  - `MainActivity`: Material 3 dashboard, Photo picker, album folder selection, and quick restoration flow.
  - `ShareReceiverActivity`: Android Share Sheet handler (`android.intent.action.SEND`, `SEND_MULTIPLE`, `image/*`) for instant restoration from Google Photos and Samsung Gallery.
  - `MatchReviewActivity`: Candidate review with confidence scores and manual selection.
  - `DiffInspectorActivity`: Side-by-side metadata comparisons.
  - `SettingsActivity`: GPS privacy policies (`KeepExact`, `Round` 1km, `Remove`, `CopyWithWarning`).
  - `AndroidStorageBridge`: ContentResolver SAF streaming and MediaStore output publishing (`Pictures/PhotoForge`).
  - `build.gradle.kts`: Gradle build configuration and Android manifest with **zero network permissions** (`INTERNET` permission absent for 100% offline guarantee).

### 🛡️ Critical Invariant Guarantees
- **`INV-01 (Source Immutability)`:** Original camera photos are opened strictly read-only and fingerprinted with SHA-256 before and after operations.
- **`INV-02 (Idempotency)`:** Namespaced migration markers (`pf:PhotoForgeMigration`) eliminate duplicate processing.
- **`INV-03 (Independent Verification)`:** Re-reads written files directly from disk to confirm EXIF, GPS, and format readability before atomic commit.
- **`INV-04 (No Silent Data Loss)`:** Unsupported/modified metadata tags are explicitly captured in diff records and warnings.
- **`INV-05 (Atomic Safety)`:** Cancelled or interrupted operations leave zero corrupt files on disk.
- **`INV-06 (Offline Guarantee)`:** Zero network requests or telemetry.

---

## 📦 Release Artifacts & Checksums

| Package | Platform / Architecture | Description |
|---|---|---|
| **`PhotoForge-Setup-v1.0.0-x64.exe`** | Windows x64 | **Standalone Windows Setup Installer** |
| `PhotoForge-v1.0.0-Windows-x64.zip` | Windows x64 | Standalone Portable WPF Desktop Application |
| `PhotoForge-v1.0.0-Windows-arm64.zip` | Windows ARM64 | Standalone Portable WPF Desktop Application |
| `PhotoForge-v1.0.0-CLI-win-x64.zip` | Windows x64 | Standalone Portable CLI tool (`photoforge.exe`) |
| `PhotoForge-v1.0.0-Android.zip` | Android | Complete Android Application Package & Source |
| `SHA256SUMS.txt` | Any | Cryptographic SHA-256 checksums |
| `sbom.spdx.json` | Any | SPDX 2.3 Software Bill of Materials |
| `photoforge.iss` | Windows | Inno Setup compiler script |

---

## 🔒 Security & Privacy Notice
PhotoForge operates **100% offline**. Zero network requests, analytics, or telemetry are ever initiated.
