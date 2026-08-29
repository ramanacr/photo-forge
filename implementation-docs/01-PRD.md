# PhotoForge Product Requirements Document

## 1. Product problem

Photo editors frequently export a modified copy without carrying forward all metadata from the camera-original asset. This can cause loss of GPS, capture date/time, camera/lens data, IPTC/XMP content, color information, and other provenance-related metadata.

The product solves this by treating the original as the metadata authority and the edited copy as the pixel/result authority.

## 2. Primary users
- Consumers editing phone/camera photos.
- Photographers processing large batches.
- Professional studios and media teams.
- Developers/automation users through CLI and future SDK.
- Privacy-sensitive users who refuse cloud processing.

## 3. Core use cases

### UC-01 Restore metadata
Original + edited photo -> output with merged metadata.

### UC-02 Restore + HEIC
Original + edited photo -> metadata-restored HEIC output.

### UC-03 Batch restoration
Folder/multiple selections -> intelligent matching -> review -> process.

### UC-04 Share-to-restore on Android
Gallery -> Share -> PhotoForge -> match original -> output copy.

### UC-05 Inspect/compare
Show source and target metadata differences before action.

### UC-06 Verify
Determine which metadata classes were copied, preserved, skipped, or failed.

### UC-07 Repeat safely
Previously processed targets are recognized by PhotoForge migration state and skipped when processing is still valid.

## 4. Functional requirements

### FR-01 Input
The application must accept files and folders through platform-native pickers and drag/drop where supported.

### FR-02 Format detection
Determine format from file structure/signature, not extension alone.

### FR-03 Metadata extraction
Extract available metadata into a canonical internal representation.

### FR-04 Matching
Rank possible originals using multiple deterministic signals.

### FR-05 Manual override
Users can accept, reject, replace or manually select the suggested original.

### FR-06 Merge
Merge metadata using the conflict rules in `03-METADATA-SPEC.md`.

### FR-07 Privacy controls
At minimum:
- Keep GPS
- Remove GPS
- Preserve metadata except selected fields
- Metadata profile selection

### FR-08 Output
Support:
- new copy
- overwrite edited target
- user choice

Default is new copy.

### FR-09 Conversion
Convert supported non-HEIC inputs to HEIC with configurable quality.

### FR-10 Verification
Verify the written output and report:
- metadata restored
- metadata preserved
- unsupported fields
- malformed fields
- pixel-change status
- migration marker

### FR-11 Marker
Write a namespaced PhotoForge migration marker where the target container permits it.

### FR-12 Idempotency
Skip targets whose marker, source fingerprint and migration profile show that the requested migration has already been applied.

### FR-13 Audit
Provide human-readable and machine-readable results.

### FR-14 Original safety
Source originals must be treated as read-only.

### FR-15 Offline
Processing must function with network disabled.

## 5. Non-functional requirements

### NFR-01 Reliability
A failed target must not corrupt an existing valid file.

Use:
1. temporary output
2. fsync/flush where applicable
3. atomic replace/rename

### NFR-02 Performance
Design for 100,000+ item batches without loading all images into RAM.

### NFR-03 Memory
Use bounded worker queues and streaming I/O.

### NFR-04 Determinism
Given the same source, target, profile and engine version, merge decisions should be reproducible.

### NFR-05 Privacy
No analytics SDK, ad SDK, remote image API, cloud matching service or remote logging dependency.

### NFR-06 Portability
Shared core must compile for Windows x64/ARM64 and Android targets.

## 6. Product tiers

### Free
- Basic restore
- HEIC conversion
- small/medium batches
- standard metadata profile

### Pro
Potential:
- unlimited batch scale
- advanced metadata profiles
- advanced matching
- audit export
- CLI
- automation
- RAW metadata support

### Enterprise
Potential:
- offline deployment package
- central policy configuration
- SDK
- support/SLA
- signed update channel
- fleet deployment
- audit integration

Do not implement licensing enforcement in the core engine. Keep entitlement checks in platform/application layers.

## 7. Acceptance criteria

A release candidate is acceptable only when:
- originals remain byte-for-byte unchanged,
- target output is readable by independent tools,
- GPS and required metadata survive round trips,
- already-processed images are correctly skipped,
- malformed metadata does not crash the process,
- batch interruption does not leave corrupted final outputs,
- offline mode is fully functional,
- no source/target photo bytes leave the device.
