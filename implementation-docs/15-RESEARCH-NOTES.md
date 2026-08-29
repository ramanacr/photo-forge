# PhotoForge Research Notes

Research checked: 2026-08-29

## HEIF/HEIC

libheif documents support for HEIC, AVIF, multiple image/auxiliary-image features, color profile handling, and reading EXIF/XMP metadata:
https://github.com/strukturag/libheif

Microsoft documents the Windows Imaging Component HEIF extension codec and HEIF compression options:
https://learn.microsoft.com/en-us/windows/win32/wic/heif-codec

Android documentation identifies HEIF-related dataspace support and current HEIF/HDR capabilities:
https://developer.android.com/reference/android/hardware/DataSpace

## Metadata library licensing

Exiv2's current repository states GPL-2.0-or-later licensing:
https://github.com/Exiv2/exiv2
https://github.com/Exiv2/exiv2/blob/main/LICENSE.txt

This makes Exiv2 a poor default dependency for a proprietary embedded core unless the distribution architecture and legal position are explicitly approved.

## JPEG

libjpeg-turbo documents compatible BSD-style/JPEG-group licensing and attribution requirements:
https://github.com/libjpeg-turbo/libjpeg-turbo/blob/main/LICENSE.md

## SQLite

SQLite's official feature page documents:
- cross-platform support including Windows and Android,
- self-contained implementation,
- public-domain source.

https://www.sqlite.org/features.html

## Engineering conclusion

For PhotoForge, the recommended dependency approach is:
- use a shared native core,
- isolate third-party codecs/metadata parsers,
- maintain a canonical internal metadata model,
- perform independent output verification,
- keep licensing obligations machine-readable in the repository.
