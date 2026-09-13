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

# 2. Generate Structured Release Notes
$notesFile = "$PSScriptRoot\RELEASE_NOTES_$tag.md"
$distDir = "$PSScriptRoot\dist"

# Generate Dynamic Artifact Manifest Table
$manifestRows = [System.Collections.Generic.List[string]]::new()
$distFiles = Get-ChildItem -Path $distDir -File | Where-Object { -not $_.Name.EndsWith(".sha256") -and $_.Name -ne "SHA256SUMS.txt" }
foreach ($file in $distFiles) {
    $sizeMB = [math]::Round($file.Length / 1MB, 2)
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $arch = if ($file.Name -match "arm64") { "Windows ARM64" } elseif ($file.Name -match "x64") { "Windows x64" } elseif ($file.Name -match "Android|\.apk") { "Android (Universal)" } else { "Multi-Platform" }
    $manifestRows.Add("| ``$($file.Name)`` | $arch | ${sizeMB} MB | ``$hash`` |")
}
$manifestTable = ($manifestRows -join "`n")

$template = @"
# PhotoForge $tag — Production Release

PhotoForge is an offline-first photo metadata continuity and modern format-conversion platform for Windows and Android.

---

## 1. Executive Summary

Release **$tag** introduces the **Metallic Radium Theme System**, fixes critical security vulnerabilities, aligns with the **INV-06 100% Offline Guarantee**, resolves Windows Explorer shell integration bugs, optimizes memory utilization for high-resolution images, and enhances SQLite database concurrency.

---

## 2. Technical Root Causes & Fixes

### 🛡️ Security Vulnerabilities & Invariant Enforcement
- **ImageSharp Vulnerability Patch (CVE / GHSA-rxmq-m78w-7wmc):**
  - *Root Cause:* Dependency ``SixLabors.ImageSharp 3.1.7`` contained an unconstrained resource allocation vulnerability.
  - *Fix:* Upgraded to ``SixLabors.ImageSharp 3.1.12``, eliminating the ``NU1902`` advisory.
- **Zip Slip Path Traversal Protection:**
  - *Root Cause:* ``InstallerEngine.ExtractPayload`` lacked canonical destination path prefix validation.
  - *Fix:* Enforced ``Path.GetFullPath`` bounds validation throwing ``SecurityException`` upon any directory escape attempt.
- **Invariant INV-06 Offline Guarantee Alignment:**
  - *Root Cause:* Automated background network calls to the GitHub API were previously executed on app launch.
  - *Fix:* Removed automatic startup network pings. Core domain engines are verified offline with zero socket dependencies; update checks are now strictly user-initiated.

### ⚡ Performance & Resource Optimization
- **Perceptual dHash Memory Optimization:**
  - *Root Cause:* Hashing 50MP+ RAW/JPEG photos allocated full uncompressed RGBA bitmaps in memory, risking OOM.
  - *Fix:* Implemented in-place downsampled resizing in C# and ``inSampleSize`` bounds decoders in Android.
- **SQLite Concurrency & Candidate Caching:**
  - *Root Cause:* SQLite database lacked Write-Ahead Logging, causing table lock contention during rapid batch runs.
  - *Fix:* Enabled ``PRAGMA journal_mode = WAL;`` and ``PRAGMA busy_timeout = 5000;`` and implemented ``GetCachedCandidateAsync`` to reuse candidate hashes.

### 🎨 Visual Experience & Shell Integration
- **Metallic Radium Theme:**
  - Adopted the full Metallic Radium design token specification across Desktop WPF, Web, Installer, and Android apps.
- **Windows Explorer Context Menu Auto-Match:**
  - Fixed command line invocation when ``--original`` is omitted by introducing ``--auto-match`` candidate discovery across related directories.

---

## 3. Artifact Manifest & Cryptographic Checksums

| Binary Asset | Architecture | Size | SHA-256 Checksum |
|---|---|---|---|
$manifestTable

> [!NOTE]
> Every release asset is accompanied by an individual ``.sha256`` checksum file alongside the master ``SHA256SUMS.txt`` file.

---

## 4. Target OS & Compatibility

| Platform | Minimum Supported Version | Architecture | Packaging |
|---|---|---|---|
| **Windows Desktop** | Windows 10 (1809+) / Windows 11 | x64 | Native Installer / Portable ZIP |
| **Windows ARM64** | Windows 11 ARM64 | ARM64 | Portable ZIP |
| **Windows CLI** | Windows 10+ / Server 2019+ | x64 | Standalone Portable ZIP |
| **Android** | Android 9.0 (API 28+) | Universal | APK / Source ZIP |

---

## 5. Security & Privacy Guarantee
PhotoForge operates **100% offline** for all photo processing and metadata operations. Zero telemetry, tracking, or background socket connections are ever established.
"@

[System.IO.File]::WriteAllText($notesFile, $template)
Write-Host "  [OK] Structured release notes generated at $notesFile" -ForegroundColor Green

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
$assets = Get-ChildItem -Path $distDir -File | ForEach-Object { $_.FullName }
Write-Host "Found $($assets.Count) release assets to upload (binaries + checksums)." -ForegroundColor Cyan

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
