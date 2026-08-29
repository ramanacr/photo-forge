# 🛡️ PhotoForge Privacy Policy

**Last Updated:** August 2026

PhotoForge is built from the ground up on the principle of **absolute local privacy and zero telemetry**.

---

## 1. Zero Data Collection & Zero Telemetry
- PhotoForge does **not** collect, store, transmit, or monetize any personal information, telemetry, analytics, device identifiers, or crash logs.
- All image metadata inspection, candidate matching, EXIF restoration, and format conversions are executed **100% locally on your device**.

## 2. No Internet Access Required
- The PhotoForge Android application deliberately requests **zero network permissions** (`android.permission.INTERNET` is not included in the manifest).
- The Windows application does not make outbound internet requests except when the user manually clicks "Check for Updates", which contacts the public GitHub Releases API solely to query the latest release version.

## 3. Storage & File Access
- PhotoForge only accesses the photo directories or files that you explicitly select using the system file picker or Storage Access Framework (SAF).
- Source photos are opened in strictly **read-only** mode (`INV-01`), and original camera files are never modified in place.

## 4. GPS & Location Privacy Controls
- PhotoForge provides granular controls for photo location metadata:
  - `KeepExact`: Retains original GPS latitude, longitude, and altitude.
  - `Round`: Fuzzes coordinates to a ~1 km radius buffer for privacy preservation.
  - `Remove`: Strips all GPS tags and location data entirely.

## 5. Contact & Open Source
PhotoForge is open-source software. The source code is publicly auditable at [github.com/ramanacr/photo-forge](https://github.com/ramanacr/photo-forge).
