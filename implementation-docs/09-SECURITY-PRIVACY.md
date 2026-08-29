# PhotoForge Security & Privacy Specification

## 1. Privacy promise

PhotoForge processing is local-only.

Core processing must not:
- upload photos,
- upload metadata,
- transmit GPS,
- call cloud AI,
- require an account,
- send analytics.

## 2. Threat model

Protect against:
- corrupt input files
- malicious image payloads
- malformed metadata
- path traversal in imported archives/providers
- symlink/reparse-point surprises
- output overwrite mistakes
- partial writes
- hostile filenames
- resource exhaustion
- unexpected codec crashes

## 3. Untrusted input

Treat every image as untrusted input.

Security controls:
- sandbox where platform permits
- fuzz decoders
- enforce image dimension/memory limits
- enforce file-size limits
- cancellation
- timeout/budget for pathological inputs
- validate output independently

## 4. Original protection

Before processing:
- resolve canonical source identity,
- ensure source != target,
- record source fingerprint,
- open source read-only.

Never edit source originals.

## 5. Output protection

Write to:
`target.tmp.photoforge.<unique-id>`

Then:
1. flush
2. validate
3. fsync where supported
4. atomic replace/rename
5. remove temp after success

## 6. GPS privacy

Default product behavior is to preserve GPS because it is the core problem being solved.

But every output flow must expose a GPS policy.

Modes:
- keep exact
- remove
- round
- keep with warning

## 7. Logs

Logs must never include:
- full EXIF dump by default
- GPS values by default
- photo bytes
- thumbnails
- personal descriptions

Safe logs:
- counts
- format
- duration
- operation ID
- error class
- metadata field names

## 8. Crash reports

Opt-in only.
No photo bytes.
No metadata payloads.
No GPS.

## 9. Supply-chain security

For each release:
- pin dependency versions
- verify upstream checksums/tags
- keep SBOM
- record licenses
- scan native libraries
- fuzz important decoders
- sign release artifacts

## 10. Privacy test

A release must pass network isolation tests:
- disable networking,
- run complete workflows,
- verify no failure caused by connectivity.

Optional advanced test:
- firewall application process,
- inspect DNS/network sockets,
- confirm no core network dependency.

## 11. Secure defaults

Default:
- create copy
- keep original untouched
- preserve GPS because requested restoration is the primary use case
- show GPS warning before share/export-like workflows
- no telemetry
- no background processing
