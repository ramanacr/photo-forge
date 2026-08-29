# PhotoForge System Architecture

## 1. Architectural style

Use a **shared native core + thin platform adapters**.

```text
                         PhotoForge
                             |
          +------------------+------------------+
          |                                     |
   PhotoForge Core                       Platform Layer
          |                       +-------------+-------------+
          |                       |                           |
   +------+------+                Windows                   Android
   |             |                 |                           |
Metadata     Matching       GUI / Explorer / CLI      UI / Share / MediaStore
   |             |              FileSystem              DocumentProvider
   +------+------+ 
          |
      Imaging
          |
   Decode / Encode
          |
   +------+------+---------+
   |      |      |         |
 JPEG   PNG    WebP   HEIF/AVIF
```

## 2. Core modules

### `photoforge-core`
Stable C/C++ ABI or C-compatible FFI boundary is recommended.

Responsibilities:
- operation orchestration
- error model
- cancellation
- progress
- metadata model
- merge engine
- fingerprinting
- matching
- verification
- migration marker
- job/report models

### `metadata-engine`
Responsibilities:
- parse supported metadata
- normalize tags
- preserve raw blocks where safe
- map between metadata models
- calculate diff
- merge
- serialize

The internal representation must be independent of a third-party library.

### `image-engine`
Responsibilities:
- format sniffing
- dimension/color-space inspection
- decode when required
- perceptual fingerprint extraction
- re-encoding for format conversion

### `heif-engine`
Responsibilities:
- HEIF container integration
- HEIC encode/decode
- EXIF/XMP integration
- auxiliary image handling where supported
- validation

### `matching-engine`
See `05-MATCHING-ENGINE.md`.

### `storage-engine`
Responsibilities:
- safe temp files
- atomic commit
- source read-only semantics
- file identity/fingerprint
- file timestamps
- conflict checks

### `audit-engine`
Responsibilities:
- structured result
- JSON result
- human-readable summary
- migration history

## 3. Suggested project layout

```text
src/
  core/
    operation/
    errors/
    progress/
    cancellation/
  metadata/
    model/
    parser/
    normalizer/
    merger/
    serializer/
    marker/
  matching/
    signals/
    scoring/
    candidate/
  imaging/
    sniff/
    decode/
    encode/
    fingerprint/
  heif/
  storage/
  audit/
  platform/
    windows/
    android/
tests/
  unit/
  integration/
  property/
  golden/
tools/
  photoforge-cli/
apps/
  windows/
  android/
```

## 4. Job pipeline

```text
DISCOVER
 -> SNIFF
 -> LOAD TARGET METADATA
 -> DETECT MARKER
 -> FIND ORIGINAL CANDIDATES
 -> SCORE
 -> REVIEW / ACCEPT
 -> EXTRACT ORIGINAL METADATA
 -> MERGE
 -> OPTIONAL GPS POLICY
 -> OPTIONAL HEIC CONVERSION
 -> WRITE TEMP
 -> VALIDATE
 -> COMMIT
 -> WRITE AUDIT
 -> COMPLETE
```

## 5. Concurrency

Use bounded parallelism:
- discovery can be parallelized,
- metadata parsing can be parallelized,
- image fingerprinting can be parallelized,
- writes must be carefully serialized per target,
- never allow two jobs to mutate the same output concurrently.

Recommended controls:
- worker pool,
- bounded queue,
- per-output lock,
- global cancellation token,
- memory budget.

## 6. Failure isolation

One malformed file must not terminate a batch.

Every item produces one of:
- `SUCCESS`
- `SUCCESS_WITH_WARNINGS`
- `SKIPPED`
- `NO_MATCH`
- `USER_REVIEW_REQUIRED`
- `UNSUPPORTED`
- `FAILED`

## 7. Versioning

Persist:
- engine version
- metadata schema version
- migration rule version
- HEIC encoder profile version

Marker example:

```xml
<pf:PhotoForgeMigration
    version="1"
    processed="true"
    sourceFingerprint="..."
    profile="standard-v1"
    engineVersion="1.0.0" />
```

Exact serialization depends on the target format.

## 8. Database

Use SQLite for local operational state:
- job history
- source/target fingerprints
- successful migrations
- errors
- user profiles
- indexes

Do not use the database as the photo store.

SQLite is documented as cross-platform, including Windows and Android. Source: https://www.sqlite.org/features.html

## 9. Platform separation

Do not place Android MediaStore logic inside core.
Do not place Windows Shell/COM logic inside core.
Do not place Windows-only codec assumptions inside core.

Use adapter interfaces.

## 10. External library policy

Third-party libraries are implementation details, not domain contracts.

For proprietary commercial binaries:
- prefer permissive libraries where practical,
- isolate LGPL components behind documented boundaries,
- avoid GPL-only libraries in the distributable core unless legal review explicitly approves the distribution model.

Exiv2 is GPL-2.0-or-later according to its current repository licensing. See `12-THIRD-PARTY-LICENSING.md`.
