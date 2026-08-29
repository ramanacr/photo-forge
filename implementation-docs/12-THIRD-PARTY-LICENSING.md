# PhotoForge Third-Party Licensing & Dependency Strategy

## 1. Objective

PhotoForge is intended to be commercially distributable as proprietary software. Dependency selection must therefore be treated as an architectural decision.

## 2. Key candidates

### libheif
Current upstream repository documents LGPL-3.0-or-later licensing and support for HEIC/AVIF plus EXIF/XMP metadata functionality.

Sources:
- https://github.com/strukturag/libheif
- https://github.com/strukturag/libheif/blob/master/libheif/api_structs.h

Policy:
- isolate behind a PhotoForge HEIF adapter,
- maintain exact upstream version and license record,
- review transitive codec dependencies separately,
- do not assume all optional encoders are under the same license.

### Exiv2
Current repository is GPLv2-or-later.

Source:
- https://github.com/Exiv2/exiv2

Policy:
- do not make Exiv2 a mandatory linked dependency of a proprietary core without legal review,
- evaluate using it as an optional tool/process only if distribution model permits,
- prefer a license-compatible alternative for embedded production core where practical.

### SQLite
SQLite is public-domain software and documents support for Windows and Android.

Source:
- https://www.sqlite.org/features.html

Policy:
- acceptable core local database candidate.

### libjpeg-turbo
Current repository documents compatible BSD-style/JPEG-group licensing with attribution requirements.

Source:
- https://github.com/libjpeg-turbo/libjpeg-turbo/blob/main/LICENSE.md

Policy:
- include required notices in documentation/materials,
- retain license files.

## 3. License process

Every dependency must have:
- exact version
- source URL
- license SPDX identifier
- source/binary distribution obligations
- static/dynamic linkage decision
- transitive dependency inventory

## 4. Avoid

Do not introduce:
- GPL-only libraries into proprietary core without legal approval,
- packages that phone home,
- libraries that require cloud processing,
- unmaintained native codecs for security-critical parsing.

## 5. SBOM

Produce SPDX or CycloneDX SBOM per release.

## 6. Legal disclaimer

This document is an engineering policy, not legal advice. Final commercial distribution should receive a qualified software-license review.
