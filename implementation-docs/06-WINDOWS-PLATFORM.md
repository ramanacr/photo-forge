# PhotoForge Windows Platform Specification

## 1. Target

Native standalone Windows application with:
- GUI
- Explorer integration
- CLI
- drag/drop
- batch workflows

Primary architectures:
- x64
- ARM64

Do not prioritize x86.

## 2. Native packaging

Target a standalone installation with no mandatory external runtime.

Distribution candidates:
- MSIX
- traditional installer
- portable ZIP
- winget
- Microsoft Store, subject to product readiness

Build artifacts:
- signed installer
- signed binaries
- SBOM
- dependency/license inventory
- SHA-256 release manifest

## 3. GUI

Main actions:
- Restore Metadata
- Restore + Convert to HEIC
- Convert to HEIC
- Inspect Metadata
- Compare Original vs Edited
- Verify
- Batch Process
- Settings

Drag/drop targets:
- edited images
- original + edited pairs
- folders

## 4. Explorer context menu

Recommended commands:
- PhotoForge: Restore Metadata
- PhotoForge: Restore + HEIC
- PhotoForge: Inspect Metadata
- PhotoForge: Verify

Avoid a noisy shell menu. Keep advanced commands under a submenu.

## 5. CLI

Example:

```powershell
photoforge restore `
  --original "D:\Photos\Originals" `
  --edited "D:\Photos\Edited" `
  --output "D:\Photos\Processed" `
  --profile standard-v1

photoforge convert `
  --input "D:\Photos" `
  --format heic `
  --quality high

photoforge verify `
  --input "D:\Photos\Processed"

photoforge inspect `
  --input "D:\Photos\Edited\IMG_001.jpg" `
  --json
```

CLI requirements:
- exit codes
- JSON output
- progress
- cancellation
- dry-run
- overwrite policy
- profile selection
- logging to local file
- no telemetry

## 6. Filesystem operations

Use Windows-native APIs for:
- long paths
- atomic rename/replace
- file locking
- volume behavior
- filesystem timestamps

Never rely on shell commands for core file mutation.

## 7. Cloud-synced folders

Treat OneDrive/Dropbox/etc. mounted directories as ordinary filesystems where possible.

Do not build cloud-provider API integrations in v1.

Potential caveat:
- a file may be placeholder-only/offline,
- sync conflicts can occur,
- filesystem mutation can trigger provider upload.

The app must clearly report filesystem-level success, not pretend it controls remote synchronization.

## 8. Windows shell safety

Explorer handler must:
- never block UI for a large operation,
- hand work to an asynchronous job process,
- show progress/results,
- respect app cancellation.

## 9. HEIC

Use PhotoForge's selected HEIF implementation for deterministic behavior rather than requiring the user to install optional codecs.

Windows WIC remains a useful interoperability fallback/validation layer.

## 10. Update system

Initial design:
- signed version manifest
- optional updater
- no update required to process images
- rollback to previous known-good version

## 11. Crash handling

No image content in crash telemetry.
Prefer local crash dumps that users explicitly export.

## 12. Accessibility

Support:
- keyboard navigation
- screen-reader labels
- high-contrast compatibility
- scalable text
- focus visibility
