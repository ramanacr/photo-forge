# PhotoForge UX Flows

## 1. Fastest flow — edited photo

```text
Drop edited photo
        |
        v
PhotoForge finds likely original
        |
        v
"Original found — 97.2% confidence"
        |
        +--> Compare
        |
        +--> Accept
              |
              v
      Metadata restored
              |
              v
      [Open Output] [Done]
```

## 2. Batch flow

```text
Select folder(s)
      |
      v
Analyze 2,413 files
      |
      v
Match results
  2,301 high confidence
     74 review
     38 no match
      |
      v
User reviews exceptions
      |
      v
Processing
      |
      v
2,288 success
    23 warnings
    74 no match / skipped
      |
      v
Export report
```

## 3. Intelligent matching review

Each row:
- target filename
- suggested original
- confidence
- reason
- metadata diff preview
- action

Actions:
- Accept
- Change Original
- No Original
- Skip

## 4. Metadata diff

Use grouped sections:
- GPS
- Capture
- Camera/Lens
- IPTC
- XMP
- Color
- Other

Example:

```text
GPS
Latitude     Original: 17.xxxx   Target: missing   -> COPY
Longitude    Original: 78.xxxx   Target: missing   -> COPY

Software
Original: Samsung Camera
Target:   Adobe Photoshop       -> KEEP TARGET

Date Taken
Original: 2025-01-02 14:33
Target:   missing               -> COPY
```

## 5. Privacy warning

When precise GPS will be placed into an output:
> This output will contain the original photo's precise location. Review privacy settings before sharing.

Provide a clear GPS control.

## 6. HEIC conversion flow

```text
Restore metadata
      |
      v
Output format
  Original
  HEIC
      |
      v
Quality
  Lossless / Very High / High / Balanced / Small / Custom
      |
      v
Preview estimated result
      |
      v
Process + Verify
```

## 7. Result language

Do not use vague "Done" alone.

Prefer:
> 184 files processed. 179 completed successfully, 3 completed with warnings, and 2 require review.

## 8. Errors

Use actionable errors:
- "Original not found"
- "Target format unsupported"
- "Metadata field cannot be represented in HEIC"
- "Output path is not writable"
- "Target changed during processing"
- "Source and target are the same file"

Never expose raw exception stacks in primary UI.

## 9. User control philosophy

Intelligence proposes.
The user decides.
The engine verifies.
The original remains untouched.
