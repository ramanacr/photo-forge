# PhotoForge v1.1.0 — Official Release

PhotoForge is an offline-first photo metadata continuity and modern format-conversion platform for Windows and Android.

---

## 🌟 What's New in v1.1.0

### 🎨 Visual Identity & Complete Branding Suite
- **Multi-Platform Icons:** High-resolution multi-layer Windows `.ico` (`16x16` to `256x256`), Android launcher mipmap icons (`mdpi` through `xxxhdpi`), and Web favicons.
- **Installer Interactive Showcase:** Setup wizard now features a live rotating feature showcase carousel highlighting key capabilities while files extract.
- **Official Brand Assets:** High-definition 16:9 hero banner and widescreen store feature graphics.

### 🖥️ Windows Native Setup Installer & Desktop Suite
- **Native Setup Installer (`PhotoForge-Setup-v1.1.0-x64.exe`):** Single-file standalone Windows installer embedding desktop and CLI binaries with zero external directory dependency.
- **WPF Desktop Application:** Modern Fluent dark UI with Quick Restore Drag & Drop, Batch Studio, Candidate Match Review, Interactive Metadata Diff Inspector, and HEIC Studio.
- **Standalone CLI (`photoforge.exe`):** Scriptable cross-platform tool with `--json`, `restore`, `convert`, `verify`, `inspect`, `match`, and `update` commands.
- **Inno Setup Script (`photoforge.iss`):** Compilable Inno Setup installer script.

### 📱 Android Application & Scoped Storage Engine
- **Android Application Suite (`com.photoforge.app`):** Full Material 3 dashboard, Scoped Storage SAF streaming, Android System Share Sheet receiver (`SEND`, `SEND_MULTIPLE`, `image/*`), and **zero network permissions** for 100% offline security.

### 🛡️ Critical Invariant Guarantees
- **`INV-01 (Source Immutability)`:** Original camera photos are opened strictly read-only and fingerprinted with SHA-256 before and after operations.
- **`INV-02 (Idempotency)`:** Namespaced migration markers (`pf:PhotoForgeMigration`) eliminate duplicate processing.
- **`INV-03 (Independent Verification)`:** Re-reads written files directly from disk to confirm EXIF, GPS, and format readability before atomic commit.
- **`INV-04 (No Silent Data Loss)`:** Unsupported/modified metadata tags are explicitly captured in diff records and warnings.
- **`INV-05 (Atomic Safety)`:** Cancelled or interrupted operations leave zero corrupt files on disk.
- **`INV-06 (Offline Guarantee)`:** Zero network requests or telemetry.

---

## 📦 Release Artifacts & Checksums

See `SHA256SUMS.txt` in release downloads for cryptographic validation.

---

## 🔒 Security & Privacy Notice
PhotoForge operates **100% offline**. Zero network requests, analytics, or telemetry are ever initiated.