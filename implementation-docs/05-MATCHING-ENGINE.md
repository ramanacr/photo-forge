# PhotoForge Original/Edited Matching Engine

## 1. Goal

Identify the most likely original for an edited target using deterministic multi-signal scoring.

The algorithm must be explainable and user-overridable.

## 2. Candidate discovery

Candidate sources can come from:
- same folder
- configured original folders
- sibling directories
- batch-selected folders
- media-provider results on Android

Do not scan an entire disk unless explicitly selected.

## 3. Signals

### S1 Filename similarity
Normalize:
- case
- separators
- common suffixes (`edited`, `edit`, `copy`, `final`, `export`)
- numeric camera sequence patterns

### S2 Timestamp similarity
Compare:
- original capture time
- target embedded time if present
- filesystem times as weak signals

Capture time should outweigh filesystem modification time.

### S3 Dimensions
Compare source dimensions to target dimensions after accounting for:
- crop
- rotate
- resize

Dimension equality is strong but not mandatory.

### S4 Metadata remnants
Use surviving target metadata to infer:
- camera model
- date
- lens
- partial GPS
- embedded description
- application export hints

### S5 Perceptual similarity
Use a perceptual fingerprint designed to survive common:
- resize
- crop
- brightness changes
- moderate color adjustments
- compression changes

### S6 Directory relation
Same directory and nearby path relationships are meaningful but should never dominate image evidence.

## 4. Scoring model

Recommended initial weighted model:

```text
score =
  0.20 * filename
+ 0.15 * timestamp
+ 0.10 * dimensions
+ 0.10 * metadata_remnants
+ 0.35 * perceptual_similarity
+ 0.10 * directory_relation
```

These weights are starting defaults, not permanent truths. They must be calibrated using fixture corpora.

## 5. Confidence bands

Suggested starting thresholds:

| Score | Decision |
|---:|---|
| >= 0.95 | Auto-accept |
| 0.85-0.949 | Suggested, highlight confidence |
| 0.70-0.849 | User review required |
| < 0.70 | No confident match |

Thresholds should be configurable internally and calibrated empirically.

## 6. Explainability

For every suggestion, expose reason codes:

```json
{
  "candidate": "IMG_1234.JPG",
  "score": 0.972,
  "reasons": [
    "same capture timestamp",
    "high perceptual similarity",
    "same camera model",
    "filename relationship"
  ]
}
```

## 7. Manual override

User actions:
- Accept suggestion
- Choose another original
- Mark target as having no original
- Reject candidate
- Review top N candidates

## 8. Avoid false-positive damage

Never auto-accept a weak match merely because a candidate exists.

When confidence is low:
- preserve target,
- produce `NO_MATCH` or `USER_REVIEW_REQUIRED`,
- do not invent metadata provenance.

## 9. Scalability

For large batches:
1. build candidate indexes,
2. use cheap filters first,
3. run expensive perceptual comparison only on a reduced set.

Index keys:
- normalized filename token
- capture date
- dimensions
- camera make/model
- folder relationship

## 10. Matching cache

Store:
- candidate fingerprint
- score
- accepted/rejected status
- engine/rule version

Do not store full image pixels in the database.

## 11. Future local ML

A local ML model may be introduced only as an optional additional signal.

The deterministic engine must remain fully functional without ML.

## 12. Golden test corpus

Build a corpus with:
- untouched originals
- crops
- rotated versions
- resized versions
- contrast/exposure changes
- color grading
- compression
- renamed files
- exported by multiple editors
- duplicate-looking images
- unrelated images with similar names

Measure:
- top-1 accuracy
- top-3 recall
- false positive rate
- no-match precision
