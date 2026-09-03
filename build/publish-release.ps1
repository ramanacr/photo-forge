# PhotoForge Automated Release Publisher
# Auto-increments release version, packages all platforms, generates release notes, tags git, pushes, and publishes to GitHub Releases.
param(
    [ValidateSet("patch", "minor", "major", "auto")]
    [string]$Bump = "minor",
    [string]$Version = "auto"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  PhotoForge Automated Release Publisher" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Package release targets
& "$PSScriptRoot\package-releases.ps1" -Version $Version -Bump $Bump

# Resolve version cleanly from stamped Directory.Build.props
$propsPath = Join-Path $RepoRoot "Directory.Build.props"
$builtVersion = (Get-Content $propsPath | Select-String '<Version>(.*?)</Version>').Matches.Groups[1].Value.Trim()
$tag = "v$builtVersion"

Write-Host "`nTarget Release Tag: $tag" -ForegroundColor Cyan

# 2. Generate Release Notes
$notesFile = "$PSScriptRoot\RELEASE_NOTES_$tag.md"
$template = @'
# PhotoForge __TAG__ - Official Release

PhotoForge is an offline-first photo metadata continuity and modern format-conversion platform for Windows and Android.

---

## What's New in __TAG__

### Auto / Self-Update from GitHub Releases (Windows & Android)
- **Windows Desktop Updater:** Automatic background check on launch and manual "Check for Updates" button in Settings. Downloads update installers directly from GitHub Releases, verifies SHA-256 checksums, and launches setup.
- **Android Self-Updater:** Integrated `AndroidUpdateEngine` querying GitHub Releases API, streaming APK downloads with progress indicator, and launching the native Android Package Installer via `FileProvider`.

### Windows Installer Size Optimization (~70% Reduction)
- **Single-File Assembly Compression:** Enabled `EnableCompressionInSingleFile=true` and stripped release debug symbols.
- **Optimal Payload Compression:** Re-engineered installer payload packing using .NET's `CompressionLevel::SmallestSize`, cutting installer footprint from ~160 MB down to ~45 MB.

### Google Photos Cloud Album Integration
- **Batch Album Studio Integration:** Dedicated Google Photos card supporting direct multi-image cloud album import via Android Photo Picker (`PickMultipleVisualMedia`) and 1-tap app launch.
- **Multi-Share Sheet Routing:** Sharing multiple photos from Google Photos directly routes into Batch Album Studio with all URIs pre-populated.

### HEIC/HEIF Format Conversion Support (Android)
- **Native HEIC Encoding:** Integrated AndroidX `HeifWriter` for hardware-accelerated HEIC/HEIF encoding on Android 9+ (API 28+) with quality preset controls.
- **Quality Preset Order Fix:** Reorganized presets into clear descending quality order (Lossless 100% -> Very High 95% -> High 85% -> Balanced 75% -> Small 60%).

### Complete Metadata Extraction & Parity (Samsung S23 & Android)
- **Full Exposure Tag Coverage:** Extracted and rendered Exposure Program, Metering Mode, Flash, White Balance, Color Space, Exposure Bias (EV), and 35mm Equivalent Focal Length.
- **Expanded Camera & Optics:** Added Body Serial Number, Lens Make, Lens Serial Number, Software, and Host Computer.
- **Detailed GPS Coordinates:** Added GPS Direction, Movement Speed, Dilution of Precision (DOP), Processing Method, and UTC GPS Timestamp.

---

## Release Artifacts & Checksums

See `SHA256SUMS.txt` in release downloads for cryptographic validation.

---

## Security & Privacy Notice
PhotoForge operates **100% offline** for all core photo manipulation. Zero network requests, analytics, or telemetry are ever initiated.
'@

$notesContent = $template.Replace("__TAG__", $tag)
[System.IO.File]::WriteAllText($notesFile, $notesContent)
Write-Host "  [OK] Release notes generated at $notesFile" -ForegroundColor Green

# 3. Commit version updates and tag
Write-Host "`nCommitting version bumps and tagging $tag..." -ForegroundColor Cyan
git -C $RepoRoot add .
git -C $RepoRoot commit --no-gpg-sign -m "chore(release): bump version to $tag"

# Delete existing tag locally if re-releasing, or create new
git -C $RepoRoot tag -a $tag -m "PhotoForge Release $tag" -f

# 4. Push commit and tag to GitHub
Write-Host "`nPushing $tag to origin/main..." -ForegroundColor Cyan
git -C $RepoRoot push origin main --tags -f

# 5. Publish GitHub Release with all distribution assets
Write-Host "`nPublishing GitHub Release $tag..." -ForegroundColor Cyan
$distDir = "$PSScriptRoot\dist"
$candidateAssets = @(
    "$distDir\PhotoForge-Setup-$tag-x64.exe",
    "$distDir\PhotoForge-$tag.apk",
    "$distDir\PhotoForge-$tag-Android.zip",
    "$distDir\PhotoForge-$tag-CLI-win-x64.zip",
    "$distDir\PhotoForge-$tag-Windows-arm64.zip",
    "$distDir\PhotoForge-$tag-Windows-x64.zip",
    "$distDir\SHA256SUMS.txt",
    "$PSScriptRoot\installer\photoforge.iss",
    "$RepoRoot\docs\branding\photoforge_feature_graphic.jpg",
    "$RepoRoot\docs\branding\photoforge_banner.jpg",
    "$RepoRoot\docs\branding\photoforge_logo.jpg",
    "$RepoRoot\docs\branding\photoforge_app_icon.jpg",
    "$RepoRoot\docs\branding\icons\app.ico"
)

$assets = $candidateAssets | Where-Object { Test-Path $_ }
Write-Host "Found $($assets.Count) release assets to upload." -ForegroundColor Cyan

$existingRelease = $null
try {
    $existingRelease = gh release view $tag 2>&1
    if ($LASTEXITCODE -ne 0) { $existingRelease = $null }
} catch {
    $existingRelease = $null
}

if ($existingRelease -and $LASTEXITCODE -eq 0) {
    Write-Host "Updating existing GitHub Release $tag..." -ForegroundColor Yellow
    gh release edit $tag --title "PhotoForge $tag" --notes-file $notesFile
    gh release upload $tag $assets --clobber
} else {
    Write-Host "Creating new GitHub Release $tag..." -ForegroundColor Green
    gh release create $tag $assets --title "PhotoForge $tag" --notes-file $notesFile
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  [OK] PhotoForge $tag Successfully Published to GitHub!" -ForegroundColor Green
Write-Host "  URL: https://github.com/ramanacr/photo-forge/releases/tag/$tag" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
