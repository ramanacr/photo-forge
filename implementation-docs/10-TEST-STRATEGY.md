# PhotoForge Test Strategy

## 1. Test layers

### Unit
- metadata normalization
- conflict rules
- GPS policy
- fingerprints
- marker parsing
- scoring
- path safety
- job state machine

### Integration
- JPEG metadata round trip
- PNG metadata round trip
- HEIC metadata round trip
- source -> target merge
- HEIC conversion
- validation

### Golden tests
Keep known-good files from real camera/editor workflows.

### Property tests
Examples:
- merge should be idempotent,
- removing GPS should always remove configured GPS fields,
- source bytes should never change,
- repeated processing with the same source/profile should produce SKIPPED.

### Fuzz tests
Fuzz:
- EXIF parser
- XMP parser
- IPTC parser
- HEIF parser
- JPEG markers
- PNG chunks
- WebP chunks

## 2. Critical invariants

### INV-01 Original immutability
Hash before and after; must match.

### INV-02 Idempotency
Second identical run should not materially modify output.

### INV-03 Verification
No result may claim success if output cannot be reopened.

### INV-04 No silent data loss
Unsupported fields must appear in warnings/report.

### INV-05 Cancellation
Cancellation cannot leave a final path containing partial output.

### INV-06 Privacy
Offline operation must not require network access.

## 3. Editor compatibility corpus

Build fixture workflows from:
- Photoshop
- Lightroom
- GIMP
- Affinity Photo
- Snapseed
- Google Photos editor
- Samsung Gallery
- Windows Photos
- Canva
- other high-value exporters

Test:
- JPEG export
- HEIC export
- PNG export
- resize
- crop
- rotate
- recompress
- color changes

## 4. Matching benchmark

Metrics:
- top-1 accuracy
- top-3 recall
- false-positive rate
- no-match accuracy
- processing time per 1k candidates

Separate benchmark sets:
- easy
- realistic
- adversarial

## 5. Performance benchmark

Benchmark:
- 1k
- 10k
- 100k images

Measure:
- wall time
- CPU
- peak RAM
- temporary disk
- throughput
- HEIC conversion throughput

## 6. Compatibility

Windows:
- x64
- ARM64
- NTFS
- removable storage
- UNC shares

Android:
- representative current Pixel/Samsung devices
- MediaStore
- content URI
- scoped storage
- HEIC support variants

## 7. Release gates

No release without:
- zero known data-corruption defects
- passing original-immutability tests
- passing migration-marker/idempotency tests
- passing offline tests
- passing supported-format smoke tests
- license inventory complete
- signed build artifacts
