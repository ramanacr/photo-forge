# PhotoForge Decision Register

| ID | Decision | Rationale |
|---|---|---|
| D-001 | Shared native core | Prevent divergent metadata semantics across platforms |
| D-002 | Windows x64 + ARM64 | Modern target coverage |
| D-003 | No x86 priority | Low strategic value |
| D-004 | Android user-triggered only | Privacy, battery and predictability |
| D-005 | Batch processing | Core use case |
| D-006 | Intelligent matching + manual override | Convenience without loss of control |
| D-007 | Original is metadata authority for capture data | Solves the actual metadata-loss problem |
| D-008 | Edited target retains edit-state metadata | Avoids destroying useful application/user information |
| D-009 | GPS explicit privacy policy | Location data is sensitive |
| D-010 | Migration marker + source fingerprint | Idempotency and auditability |
| D-011 | SQLite local history | Cross-platform local operational state |
| D-012 | HEIC quality configurable | Different users prioritize quality vs size |
| D-013 | HEIC metadata-aware pipeline | Avoid metadata loss during conversion |
| D-014 | No cloud/telemetry | Core product trust proposition |
| D-015 | CLI included | Professional/automation use |
| D-016 | Explorer integration | Fast desktop workflow |
| D-017 | Video deferred | Prevent scope explosion |
| D-018 | RAW pixel processing deferred | Preserve v1 focus |
| D-019 | Local AI optional only | Core matching must remain deterministic/offline |
| D-020 | New-copy default output | Safest consumer behavior |
| D-021 | Original never modified | Strong safety invariant |
| D-022 | Product category is metadata continuity | Stronger positioning than generic EXIF editor |
