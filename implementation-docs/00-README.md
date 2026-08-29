# PhotoForge — Product & Engineering Specification Pack

## Status
Baseline specification — 2026-08-29

## Product
**PhotoForge** is an offline-first photo metadata continuity and format-conversion platform for Windows and Android.

**Positioning:** Edit freely. Keep everything.

Core promise:

> Identify the original photo behind an edited copy, restore the original metadata safely, preserve edited-file metadata when appropriate, optionally convert the result to HEIC, verify the outcome, and mark the target so the same migration is not unnecessarily repeated.

## Primary clients
- PhotoForge Desktop — Windows native application
- PhotoForge Android — Android application
- PhotoForge CLI — Windows command-line interface
- PhotoForge Core — shared deterministic native engine

## Product principles
1. 100% local photo processing.
2. No cloud upload.
3. No mandatory account.
4. No telemetry.
5. Original files are never modified.
6. Metadata operations are idempotent.
7. User can override every intelligent match.
8. Never silently discard metadata.
9. Verification is a first-class operation.
10. HEIC conversion is metadata-aware and quality configurable.

## Documentation map
- `01-PRD.md` — product requirements and scope
- `02-SYSTEM-ARCHITECTURE.md` — architecture and component boundaries
- `03-METADATA-SPEC.md` — canonical metadata model and merge rules
- `04-FORMAT-COMPATIBILITY-MATRIX.md` — formats and metadata capabilities
- `05-MATCHING-ENGINE.md` — original/edited matching and confidence system
- `06-WINDOWS-PLATFORM.md` — native Windows application, Explorer, CLI
- `07-ANDROID-PLATFORM.md` — Android UX, storage and sharing
- `08-UX-FLOWS.md` — user journeys and screen-level requirements
- `09-SECURITY-PRIVACY.md` — threat model, privacy and filesystem safety
- `10-TEST-STRATEGY.md` — testing, fixtures, property/integration tests
- `11-ROADMAP-COMMERCIALIZATION.md` — release phases and commercial path
- `12-THIRD-PARTY-LICENSING.md` — dependency and distribution considerations
- `13-AGENT-IMPLEMENTATION-GUIDE.md` — coding-agent execution contract

## Recommended v1
### Must-have
- JPEG, PNG, WebP, TIFF, BMP, GIF, AVIF, HEIC/HEIF
- DNG metadata-only support where safely possible
- EXIF, GPS, XMP, IPTC, ICC and supported container metadata
- Intelligent original/edited matching
- Manual override
- Batch processing
- Metadata-only restoration where technically possible
- HEIC conversion with configurable quality
- Migration marker + source fingerprint
- Verification/audit result
- Windows GUI + Explorer + CLI
- Android app + share flow + batch selection
- x64 + ARM64 Windows builds
- Offline/no telemetry/no cloud

### Deferred
- Full RAW pixel development/conversion
- Video metadata workflows
- Live Photos / Motion Photos
- Background watchers
- Cloud-provider APIs
- Cloud AI
- Online accounts

## Important technical position
Do not make PhotoForge's core semantics dependent on Windows WIC, Android Media APIs, or a single metadata library. Platform services are adapters around a shared PhotoForge model.

HEIF/HEIC is an image-container format with codec and metadata nuances. Treat HEIC output as a pipeline with decode/encode + metadata migration + validation rather than as a filename conversion.

## Research notes
- libheif supports HEIC/AVIF and can read EXIF/XMP metadata; it also supports auxiliary images and image sequences. See: https://github.com/strukturag/libheif
- Windows WIC exposes Microsoft's HEIF extension codec and HEIF compression options. See: https://learn.microsoft.com/en-us/windows/win32/wic/heif-codec
- SQLite is cross-platform and explicitly supports Windows and Android; sources are public domain. See: https://www.sqlite.org/features.html
- Exiv2 supports broad image metadata work but is GPL-2.0-or-later, so licensing must be evaluated before embedding it in proprietary binaries. See: https://github.com/Exiv2/exiv2
- libheif is LGPL-3.0-or-later; codec plugins/optional encoders can have separate licenses and must be reviewed independently. See: https://github.com/strukturag/libheif

## Non-goals for v1
PhotoForge is not:
- a photo editor,
- a cloud backup product,
- a DAM replacement,
- a social photo network,
- a general file synchronization engine.
