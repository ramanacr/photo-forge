# PhotoForge Android Platform Specification

## 1. Product mode

Android is user-triggered only.

No background watcher or automatic gallery monitor in v1.

## 2. Entry points

### A. App launch
- Choose edited images
- Choose originals
- Choose original folder / provider location where permissions permit

### B. Android Share
```text
Gallery
  -> Share
  -> PhotoForge
  -> analyze
  -> find original
  -> review
  -> process
```

### C. Multi-select batch
Use the Android picker/media APIs appropriate to supported API levels.

## 3. Storage model

Prefer:
- MediaStore
- Storage Access Framework / DocumentProvider
- app-private temporary files

Avoid broad filesystem assumptions.

The app should request the narrowest permissions required.

## 4. Output

Offer:
- create copy
- replace edited file, when platform permissions and file ownership allow
- user choice

Default: create copy.

## 5. Android UI

Suggested screens:
1. Home
2. Select photos
3. Matching review
4. Metadata preview/diff
5. Options
6. Processing
7. Results
8. Verify/detail

## 6. Share flow behavior

On share:
- import/share reference safely,
- do not assume the shared item is a permanent filesystem path,
- resolve a content URI,
- copy only into app-private working storage as needed,
- process,
- publish output back to MediaStore.

## 7. HEIC

Use platform capabilities when reliable for the target API/device, but keep the metadata model independent.

Android documents HEIF/HEIC-related media support through its media and dataspace APIs. Verify encoder capability on each supported API tier and device class.

## 8. Offline guarantee

Turn on airplane mode during testing and confirm:
- selection works,
- matching works,
- processing works,
- output works,
- verification works,
- no unexpected network permission or dependency is required for core functionality.

## 9. Android performance

Do not load all selected images at full resolution.

Use:
- bounds-only inspection,
- thumbnails,
- streaming,
- bounded coroutines/workers,
- cancellation,
- battery-aware batching.

## 10. App lifecycle

A job should survive ordinary configuration changes where practical.

Use a local job state so UI can reconnect to an in-progress foreground processing job without losing the logical operation state.

This is not background monitoring; it is lifecycle resilience for a user-initiated operation.

## 11. Privacy

Do not request:
- location permission merely to read photo GPS metadata,
- contacts,
- microphone,
- unrelated storage permissions.

The app reads embedded metadata from the selected photo rather than using the phone's current physical location.

## 12. Android device matrix

Minimum validation should include:
- recent Pixel-class device
- recent Samsung device
- Android emulator for API coverage
- HEIC input/output where device supports it
- MediaStore edge cases
- content URI providers
