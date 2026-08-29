# PhotoForge Multi-Platform Release Packaging Script
param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = "$PSScriptRoot\dist"
)

$ErrorActionPreference = "Stop"
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  PhotoForge Release Builder v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$RepoRoot = Split-Path -Parent $PSScriptRoot

# 1. Publish Windows x64 Standalone Desktop
Write-Host "`n[1/4] Publishing Windows x64 Desktop Application..." -ForegroundColor Yellow
$Win64Dir = "$OutputDir\PhotoForge-Windows-x64"
dotnet publish "$RepoRoot\apps\PhotoForge.Desktop\PhotoForge.Desktop.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $Win64Dir

Compress-Archive -Path "$Win64Dir\*" -DestinationPath "$OutputDir\PhotoForge-v$Version-Windows-x64.zip" -Force
Remove-Item -Recurse -Force $Win64Dir

# 2. Publish Windows ARM64 Standalone Desktop
Write-Host "`n[2/4] Publishing Windows ARM64 Desktop Application..." -ForegroundColor Yellow
$WinArm64Dir = "$OutputDir\PhotoForge-Windows-arm64"
dotnet publish "$RepoRoot\apps\PhotoForge.Desktop\PhotoForge.Desktop.csproj" `
    -c Release `
    -r win-arm64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $WinArm64Dir

Compress-Archive -Path "$WinArm64Dir\*" -DestinationPath "$OutputDir\PhotoForge-v$Version-Windows-arm64.zip" -Force
Remove-Item -Recurse -Force $WinArm64Dir

# 3. Publish PhotoForge CLI Tool
Write-Host "`n[3/4] Publishing PhotoForge CLI Cross-Platform Package..." -ForegroundColor Yellow
$CliDir = "$OutputDir\PhotoForge-CLI"
dotnet publish "$RepoRoot\tools\PhotoForge.Cli\PhotoForge.Cli.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $CliDir

Compress-Archive -Path "$CliDir\*" -DestinationPath "$OutputDir\PhotoForge-v$Version-CLI-win-x64.zip" -Force
Remove-Item -Recurse -Force $CliDir

# 4. Package Android Platform Layer
Write-Host "`n[4/4] Packaging PhotoForge Android Package..." -ForegroundColor Yellow
$AndroidDir = "$OutputDir\PhotoForge-Android"
dotnet publish "$RepoRoot\apps\PhotoForge.Android\PhotoForge.Android.csproj" `
    -c Release `
    -o $AndroidDir

Compress-Archive -Path "$AndroidDir\*" -DestinationPath "$OutputDir\PhotoForge-v$Version-Android.zip" -Force
Remove-Item -Recurse -Force $AndroidDir

# 5. Generate SHA-256 Checksums
Write-Host "`nGenerating SHA-256 release checksums..." -ForegroundColor Yellow
$ChecksumFile = "$OutputDir\SHA256SUMS.txt"
$ReleaseFiles = Get-ChildItem "$OutputDir\*.zip"

$Checksums = @()
foreach ($file in $ReleaseFiles) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $entry = "$hash  $($file.Name)"
    $Checksums += $entry
    Write-Host "  $entry" -ForegroundColor Green
}

$Checksums | Out-File -FilePath $ChecksumFile -Encoding utf8

Write-Host "`n[SUCCESS] Build and Packaging Complete! Release artifacts ready in: $OutputDir" -ForegroundColor Green
