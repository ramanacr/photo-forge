# PhotoForge User Guide

PhotoForge restores missing original photo metadata (capture timestamps, GPS location, camera equipment, exposure parameters, lens specifications) to edited photos and copies without ever modifying your camera originals.

---

## Key Invariant Guarantees

1. **Original Immutability (`INV-01`):** Camera originals are opened in strictly read-only mode with cryptographic SHA-256 fingerprint verification before and after every operation.
2. **Deterministic Multi-Signal Matching:** Automatically links edited photos to originals by combining filename tokens, capture timestamps, aspect ratios, surviving EXIF remnants, perceptual visual hashes (dHash), and folder structures.
3. **Full User Control & Override:** Auto-accepts high-confidence matches while prompting for review on borderline matches.
4. **GPS Privacy Engine:** Choose between full precision GPS preservation, stripping GPS entirely, or 1km coordinate rounding.
5. **Idempotency & Migration Markers (`INV-02`):** Injects standard-compliant, namespaced migration markers (`PF-MIG`) to prevent redundant re-processing.
6. **Modern Format Studio:** Converts photos to modern WebP/JPEG/PNG formats with configurable quality presets while retaining complete metadata continuity.
7. **100% Local & Offline (`INV-06`):** Never transmits photos, metadata, or telemetry over the internet.

---

## 📱 Android App Guide

### 1. Quick Metadata Restore
- Tap **Select Pair & Restore**.
- Pick your edited photo from the file/gallery picker.
- Pick the matching original camera photo.
- PhotoForge will restore camera equipment, capture timestamp, and GPS coordinates directly into the photo and save the output to `Pictures/PhotoForge`.

### 2. Batch Album Studio
- Tap **Open Batch Studio**.
- Select multiple edited photos (or a folder).
- Select the pool of candidate original camera photos.
- Toggle **Auto-Accept High Confidence Matches (≥75%)**.
- Tap **Start Batch Processing** to watch real-time progress and receive a comprehensive execution summary.

### 3. Format Converter Studio
- Tap **Format Converter**.
- Pick a photo to convert.
- Select your target container format (**WebP**, **JPEG**, or **PNG**) and choose a quality preset (**Lossless**, **Very High 95%**, **High 85%**, **Balanced 75%**, **Small 60%**).
- Keep **Preserve All Metadata** enabled.
- Tap **Convert & Save to Gallery**.

### 4. Metadata Inspector
- Tap **Metadata Inspector**.
- Select any photo to inspect technical specs, camera make/model, lens details, ISO/shutter/aperture exposure settings, GPS coordinates with elevation, IPTC keywords, and PhotoForge Provenance markers.

### 5. Continuity Verifier
- Tap **Continuity Verifier**.
- Pick any photo to independently verify bitmap decodability, pixel dimension validity, EXIF tag preservation, and migration marker consistency.

### 6. Candidate Match Finder
- Tap **Candidate Match Finder**.
- Select the edited photo and then select a pool of original photos.
- Review ranked candidates with confidence badges (Auto-Accept, Suggested, Review Required) and visual signal breakdowns.
- Tap **Restore Using This Original** on any candidate.

### 7. Share Sheet Integration
- In Google Photos or your default gallery app, tap **Share** on any edited photo and select **PhotoForge Restore**.
- Select the original camera photo to instantly restore provenance.

---

## 🖥️ Desktop Quick Start

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
