# PhotoForge User Guide

PhotoForge restores missing original photo metadata (capture timestamps, GPS location, camera equipment, exposure parameters, lens specifications) to edited photos and copies without ever modifying your camera originals.

---

## Key Features

1. **Original Immutability:** Camera originals are opened in strictly read-only mode with cryptographic SHA-256 fingerprint verification before and after every operation.
2. **Deterministic Multi-Signal Matching:** Automatically links edited photos to originals by combining filename tokens, capture timestamps, aspect ratios, surviving EXIF remnants, perceptual visual hashes (dHash), and folder structures.
3. **Full User Control & Override:** Auto-accepts high-confidence matches while prompting for review on borderline matches.
4. **GPS Privacy Engine:** Choose between full precision GPS preservation, stripping GPS entirely, or 1km coordinate rounding.
5. **Idempotency & Migration Markers:** Injects standard-compliant, namespaced migration markers to prevent redundant re-processing.
6. **HEIC Conversion Studio:** Converts photos to modern HEIC/WebP format with configurable quality modes while retaining complete metadata continuity.
7. **100% Local & Offline:** Never transmits photos, metadata, or telemetry over the internet.

---

## Desktop Quick Start

1. **Launch PhotoForge Desktop:**
   Open `PhotoForge.Desktop.exe`.
2. **Quick Restore:**
   Drag and drop an original photo into the left panel and the edited copy into the right panel. Click **⚡ Restore Metadata**.
3. **Batch Mode:**
   Select the directory containing your edited images and the folder containing your camera originals. Click **Run Batch Migration**.
4. **Inspect Metadata Diff:**
   Review exact fields copied from the original and preserved from the edited target.
5. **Explorer Context Menu:**
   Enable Windows Explorer integration in **Settings** to restore photos directly by right-clicking images in File Explorer.
