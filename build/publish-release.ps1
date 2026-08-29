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
# PhotoForge __TAG__ — Official Release

PhotoForge is an offline-first photo metadata continuity and modern format-conversion platform for Windows and Android.

---

## 🌟 What's New in __TAG__

### 🎨 Visual Identity & Complete Branding Suite
- **Multi-Platform Icons:** High-resolution multi-layer Windows `.ico` (`16x16` to `256x256`), Android launcher mipmap icons (`mdpi` through `xxxhdpi`), and Web favicons.
- **Installer Interactive Showcase:** Setup wizard now features a live rotating feature showcase carousel highlighting key capabilities while files extract.
- **Official Brand Assets:** High-definition 16:9 hero banner and widescreen store feature graphics.

### 🖥️ Windows Native Setup Installer & Desktop Suite
- **Native Setup Installer (`PhotoForge-Setup-__TAG__-x64.exe`):** Single-file standalone Windows installer embedding desktop and CLI binaries with zero external directory dependency.
- **WPF Desktop Application:** Modern Fluent dark UI with Quick Restore Drag & Drop, Batch Studio, Candidate Match Review, Interactive Metadata Diff Inspector, and HEIC Studio.
- **Standalone CLI (`photoforge.exe`):** Scriptable cross-platform tool with `--json`, `restore`, `convert`, `verify`, `inspect`, `match`, and `update` commands.
- **Inno Setup Script (`photoforge.iss`):** Compilable Inno Setup installer script.

### 📱 Android Application & Scoped Storage Engine
- **Android Application Suite (`com.photoforge.app`):** Full Material 3 dashboard, Scoped Storage SAF streaming, Android System Share Sheet receiver (`SEND`, `SEND_MULTIPLE`, `image/*`), and **zero network permissions** for 100% offline security.

### 🛡️ Critical Invariant Guarantees
- **`INV-01 (Source Immutability)`:** Original camera photos are opened strictly read-only and fingerprinted with SHA-256 before and after operations.
- **`INV-02 (Idempotency)`:** Namespaced migration markers (`pf:PhotoForgeMigration`) eliminate duplicate processing.
- **`INV-03 (Independent Verification)`:** Re-reads written files directly from disk to confirm EXIF, GPS, and format readability before atomic commit.
- **`INV-04 (No Silent Data Loss)`:** Unsupported/modified metadata tags are explicitly captured in diff records and warnings.
- **`INV-05 (Atomic Safety)`:** Cancelled or interrupted operations leave zero corrupt files on disk.
- **`INV-06 (Offline Guarantee)`:** Zero network requests or telemetry.

---

## 📦 Release Artifacts & Checksums

See `SHA256SUMS.txt` in release downloads for cryptographic validation.

---

## 🔒 Security & Privacy Notice
PhotoForge operates **100% offline**. Zero network requests, analytics, or telemetry are ever initiated.
'@

$notesContent = $template.Replace("__TAG__", $tag)
[System.IO.File]::WriteAllText($notesFile, $notesContent)
Write-Host "  ✔ Release notes generated at $notesFile" -ForegroundColor Green

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
$assets = @(
    "$distDir\PhotoForge-Setup-$tag-x64.exe",
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

$existingRelease = gh release view $tag 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Updating existing GitHub Release $tag..." -ForegroundColor Yellow
    gh release edit $tag --title "PhotoForge $tag" --notes-file $notesFile
    gh release upload $tag $assets --clobber
} else {
    Write-Host "Creating new GitHub Release $tag..." -ForegroundColor Green
    gh release create $tag $assets --title "PhotoForge $tag" --notes-file $notesFile
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  ✔ PhotoForge $tag Successfully Published to GitHub!" -ForegroundColor Green
Write-Host "  URL: https://github.com/ramanacr/photo-forge/releases/tag/$tag" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
