# PhotoForge Coding-Agent Implementation Guide

## 1. Agent mission

Implement PhotoForge as a production-grade offline photo metadata continuity platform for Windows and Android.

The agent must treat the documents in this pack as the product source of truth.

## 2. Non-negotiable rules

1. Never modify original source photos.
2. Never upload photo data.
3. Never add telemetry.
4. Never require network connectivity for core processing.
5. Never silently discard unsupported metadata.
6. Never auto-accept weak original matches.
7. Always allow manual match override.
8. Always validate output before commit.
9. Make operations idempotent.
10. Keep platform-specific code out of the core domain model.

## 3. Implementation order

### Step 1 — Repository foundation
Create:
```text
/src
/tests
/apps
/tools
/docs
/build
```

Set up:
- formatting
- linting
- static analysis
- sanitizer builds
- CI
- dependency pinning

### Step 2 — Domain contracts
Implement:
- `PhotoFormat`
- `PhotoRef`
- `MetadataDocument`
- `MetadataField`
- `MetadataDiff`
- `MigrationMarker`
- `MatchingCandidate`
- `MatchingDecision`
- `OperationResult`
- `VerificationResult`

### Step 3 — Storage safety
Implement:
- read-only source opening
- temporary outputs
- atomic commit
- cancellation
- conflict detection

Write tests before integrating codecs.

### Step 4 — Metadata engine
Implement canonical metadata categories:
- EXIF
- GPS
- IPTC
- XMP
- ICC
- timestamps
- camera/lens
- supported MakerNotes

Keep parser/serializer dependencies behind interfaces.

### Step 5 — Merge policy
Implement:
- default profile
- field classes
- GPS policies
- conflict handling
- diff
- warnings

### Step 6 — Migration marker
Implement:
- marker read
- marker write
- version compatibility
- source fingerprint
- idempotency

### Step 7 — Matching
Implement:
- candidate discovery
- filename score
- timestamp score
- dimension score
- metadata clue score
- perceptual score
- directory score
- aggregate score
- explanation
- manual override

### Step 8 — Verification
Implement independent re-open/re-read where practical.

Never verify solely from in-memory state.

### Step 9 — HEIC
Add HEIF adapter only after metadata core is stable.

Pipeline:
```text
read -> decode -> capture metadata -> encode -> inject -> reopen -> verify
```

### Step 10 — CLI
Implement commands:
```text
restore
convert
verify
inspect
batch
```

Every command supports JSON output.

### Step 11 — Windows app
Implement GUI around core API.

Add Explorer integration after core UX works.

### Step 12 — Android app
Implement:
- picker
- share intent
- MediaStore publishing
- batch UI
- lifecycle-safe foreground processing

## 4. Definition of Done for every feature

A feature is complete only when:
- unit tests exist,
- integration tests exist where applicable,
- failure cases are tested,
- cancellation is tested,
- logs are privacy-safe,
- docs are updated,
- CLI/API behavior is versioned,
- no source mutation occurs.

## 5. Agent working style

Before changing architecture:
- inspect current code,
- identify existing contracts,
- preserve stable interfaces,
- write a concise design note for major changes.

Prefer vertical slices:
> metadata extraction -> merge -> write -> verify

over building huge abstractions first.

## 6. Dependency rule

Do not add a native dependency merely because it is convenient.

Before adding one:
- inspect license,
- inspect maintenance status,
- inspect security posture,
- determine static/dynamic linking implications,
- record it in `12-THIRD-PARTY-LICENSING.md`.

## 7. Error model

Errors must be typed.

Example categories:
```text
InvalidInput
UnsupportedFormat
MetadataParseFailure
MetadataWriteFailure
NoMatch
LowConfidenceMatch
OutputConflict
AtomicCommitFailure
VerificationFailure
Cancelled
InternalError
```

User messages are separate from developer diagnostics.

## 8. Observability

No remote telemetry.

Use:
- local structured logs,
- local job history,
- reproducible operation IDs.

## 9. Performance rules

Do not:
- decode every image at full size,
- keep entire batches in memory,
- compute expensive perceptual comparisons against every candidate.

Use staged filtering and bounded concurrency.

## 10. Security rules

Fuzz all native decoders.
Treat image files as hostile inputs.
Use sanitizers during development.
Keep codecs isolated.
Validate all outputs.

## 11. Release checklist

```text
[ ] x64 build
[ ] ARM64 build
[ ] Android build
[ ] CLI build
[ ] code signing
[ ] dependency inventory
[ ] SBOM
[ ] license notices
[ ] unit tests
[ ] integration tests
[ ] golden fixtures
[ ] fuzz regression
[ ] performance benchmark
[ ] offline test
[ ] source immutability test
[ ] migration/idempotency test
[ ] release notes
```

## 12. Suggested first milestones

Milestone M1:
- repository
- domain contracts
- metadata model
- storage safety

Milestone M2:
- JPEG metadata restore
- diff
- verification
- marker

Milestone M3:
- matching engine
- batch
- CLI

Milestone M4:
- HEIC conversion

Milestone M5:
- Windows GUI/Explorer

Milestone M6:
- Android

## 13. Do not prematurely optimize branding

Use `PhotoForge` as a working codename until trademark/domain/store checks are complete.
