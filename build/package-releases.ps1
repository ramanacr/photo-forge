# PhotoForge Multi-Platform Release Packaging Script
# Supports automated semantic version incrementing (-Bump patch|minor|major or -Version auto)
param(
    [string]$Version = "auto",
    [ValidateSet("patch", "minor", "major", "auto")]
    [string]$Bump = "minor",
    [string]$OutputDir = "$PSScriptRoot\dist"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

# Import Semantic Version Manager
. "$PSScriptRoot\version-manager.ps1"

# 1. Resolve Dynamic / Auto-Incremented Version
if ($Version -eq "auto" -or [string]::IsNullOrWhiteSpace($Version)) {
    $latest = Get-LatestReleaseVersion -RepoRoot $RepoRoot
    $Version = Get-NextVersion -CurrentVersion $latest -Bump $Bump
    Write-Host "Auto-incremented release version from v$latest to v$Version ($Bump bump)" -ForegroundColor Green
}

# Apply version stamp across projects
Set-ProjectVersions -Version $Version -RepoRoot $RepoRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  PhotoForge Release Builder v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# 1. Publish Windows x64 Standalone Desktop & CLI Payload
Write-Host "`n[1/5] Publishing Windows x64 Desktop Application & CLI (Compressed SingleFile)..." -ForegroundColor Yellow
$Win64Dir = "$OutputDir\PhotoForge-Windows-x64"
dotnet publish "$RepoRoot\apps\PhotoForge.Desktop\PhotoForge.Desktop.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $Win64Dir

# Also publish CLI tool to the same package so both Desktop and CLI are bundled
dotnet publish "$RepoRoot\tools\PhotoForge.Cli\PhotoForge.Cli.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $Win64Dir

# Copy app icon to payload
if (Test-Path "$RepoRoot\apps\PhotoForge.Desktop\app.ico") {
    Copy-Item "$RepoRoot\apps\PhotoForge.Desktop\app.ico" "$Win64Dir\app.ico" -Force
}

# Remove publish artifacts that shouldn't ship in the installer
Write-Host "  Cleaning up publish artifacts..." -ForegroundColor Gray
Get-ChildItem -Path $Win64Dir -Include *.pdb, *.xml, *.deps.json, *.runtimeconfig.json -Recurse | Remove-Item -Force

# Create Windows x64 ZIP with optimal compression
# Create Windows x64 ZIP with optimal compression
$WinZipPath = "$OutputDir\PhotoForge-v$Version-Windows-x64.zip"
Compress-Archive -Path "$Win64Dir\*" -DestinationPath $WinZipPath -CompressionLevel Optimal -Force

# Create Payload.zip for the Setup Installer
$InstallerPayloadPath = "$RepoRoot\apps\PhotoForge.Installer\Payload.zip"
Copy-Item $WinZipPath $InstallerPayloadPath -Force

# 2. Publish Windows Native Installer EXE with embedded Payload
Write-Host "`n[2/5] Publishing Windows Native Setup Installer (embedding compressed Payload.zip)..." -ForegroundColor Yellow
$InstallerDir = "$OutputDir\PhotoForge-Installer-Build"
dotnet publish "$RepoRoot\apps\PhotoForge.Installer\PhotoForge.Installer.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $InstallerDir

$BuiltSetupExe = Get-ChildItem -Path $InstallerDir -Filter "PhotoForge-Setup-*.exe" | Select-Object -First 1
if ($BuiltSetupExe) {
    Copy-Item $BuiltSetupExe.FullName "$OutputDir\PhotoForge-Setup-v$Version-x64.exe" -Force
} else {
    Copy-Item "$InstallerDir\PhotoForge-Setup-v$Version-x64.exe" "$OutputDir\PhotoForge-Setup-v$Version-x64.exe" -Force
}

Remove-Item -Recurse -Force $InstallerDir
Remove-Item -Recurse -Force $Win64Dir
if (Test-Path $InstallerPayloadPath) {
    Remove-Item -Force $InstallerPayloadPath
}

# 3. Publish Windows ARM64 Desktop Application
Write-Host "`n[3/5] Publishing Windows ARM64 Desktop Application..." -ForegroundColor Yellow
$WinArm64Dir = "$OutputDir\PhotoForge-Windows-arm64"
dotnet publish "$RepoRoot\apps\PhotoForge.Desktop\PhotoForge.Desktop.csproj" `
    -c Release `
    -r win-arm64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $WinArm64Dir

# Remove publish artifacts that shouldn't ship
Get-ChildItem -Path $WinArm64Dir -Include *.pdb, *.xml, *.deps.json, *.runtimeconfig.json -Recurse | Remove-Item -Force

$Arm64ZipPath = "$OutputDir\PhotoForge-v$Version-Windows-arm64.zip"
Compress-Archive -Path "$WinArm64Dir\*" -DestinationPath $Arm64ZipPath -CompressionLevel Optimal -Force
Remove-Item -Recurse -Force $WinArm64Dir

# 4. Publish CLI Cross-Platform Package
Write-Host "`n[4/5] Publishing PhotoForge CLI Cross-Platform Package..." -ForegroundColor Yellow
$CliDir = "$OutputDir\PhotoForge-CLI"
dotnet publish "$RepoRoot\tools\PhotoForge.Cli\PhotoForge.Cli.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $CliDir

# Remove publish artifacts that shouldn't ship
Get-ChildItem -Path $CliDir -Include *.pdb, *.xml, *.deps.json, *.runtimeconfig.json -Recurse | Remove-Item -Force

$CliZipPath = "$OutputDir\PhotoForge-v$Version-CLI-win-x64.zip"
Compress-Archive -Path "$CliDir\*" -DestinationPath $CliZipPath -CompressionLevel Optimal -Force
Remove-Item -Recurse -Force $CliDir

# 5. Package Android Application
Write-Host "`n[5/5] Packaging PhotoForge Android Application..." -ForegroundColor Yellow
$AndroidProjectDir = "$RepoRoot\apps\PhotoForge.Android"

$GradleWrapper = "$AndroidProjectDir\gradlew.bat"
if (-not (Test-Path $GradleWrapper)) {
    $GradleWrapper = "$AndroidProjectDir\gradlew"
}

if (Test-Path $GradleWrapper) {
    if (-not (Test-Path "$env:JAVA_HOME\bin\java.exe")) {
        if (Test-Path "C:\Program Files\Java\jdk-21\bin\java.exe") {
            $env:JAVA_HOME = "C:\Program Files\Java\jdk-21"
        } elseif (Test-Path "C:\Program Files\Java\jdk-17\bin\java.exe") {
            $env:JAVA_HOME = "C:\Program Files\Java\jdk-17"
        }
    }
    if (-not $env:ANDROID_HOME -or -not (Test-Path $env:ANDROID_HOME)) {
        if (Test-Path "$env:LOCALAPPDATA\Android\Sdk") {
            $env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
        }
    }

    try {
        Write-Host "  Building Android APK via Gradle..." -ForegroundColor Gray
        try {
            & $GradleWrapper -p $AndroidProjectDir assembleRelease --no-daemon 2>&1 | Out-Null
        } catch { }

        # Locate the built APK
        $ApkFile = Get-ChildItem -Path "$AndroidProjectDir\build\outputs\apk" -Filter "*.apk" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($ApkFile) {
            Copy-Item $ApkFile.FullName "$OutputDir\PhotoForge-v$Version.apk" -Force
            Write-Host "  [OK] APK packaged: PhotoForge-v$Version.apk" -ForegroundColor Green
        } elseif (Test-Path "$OutputDir\PhotoForge-v$Version.apk") {
            Write-Host "  [OK] Using existing APK: PhotoForge-v$Version.apk" -ForegroundColor Green
        } else {
            Write-Host "  [WARN] Gradle build completed without generating an APK." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  [WARN] Android APK build skipped" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [WARN] Gradle wrapper not found. Skipping APK build." -ForegroundColor Yellow
}

# Also package Android source + .NET bridge assemblies as a zip for reference
$AndroidDistDir = "$OutputDir\PhotoForge-Android-Source"
if (Test-Path $AndroidDistDir) { Remove-Item -Recurse -Force $AndroidDistDir }
New-Item -ItemType Directory -Force -Path $AndroidDistDir | Out-Null
Copy-Item -Recurse -Path "$AndroidProjectDir\src" -Destination "$AndroidDistDir\src" -Force
Copy-Item -Path "$AndroidProjectDir\build.gradle.kts" -Destination "$AndroidDistDir\build.gradle.kts" -Force
Copy-Item -Path "$AndroidProjectDir\PhotoForge.Android.csproj" -Destination "$AndroidDistDir\PhotoForge.Android.csproj" -Force

$bridgeBin = "$AndroidProjectDir\bin\Release\net9.0"
if (Test-Path $bridgeBin) {
    $binDest = "$AndroidDistDir\binaries"
    New-Item -ItemType Directory -Force -Path $binDest | Out-Null
    Copy-Item -Recurse -Path "$bridgeBin\*" -Destination $binDest -Force
}

$AndroidZipPath = "$OutputDir\PhotoForge-v$Version-Android.zip"
if (Test-Path $AndroidZipPath) { Remove-Item -Force $AndroidZipPath }

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($AndroidDistDir, $AndroidZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
} catch {
    Compress-Archive -Path "$AndroidDistDir\*" -DestinationPath $AndroidZipPath -CompressionLevel Optimal -Force
}
Remove-Item -Recurse -Force $AndroidDistDir -ErrorAction SilentlyContinue

# 6. Generate SHA-256 Checksums
Write-Host "`nGenerating SHA-256 release checksums..." -ForegroundColor Cyan
$ChecksumFile = "$OutputDir\SHA256SUMS.txt"
if (Test-Path $ChecksumFile) { Remove-Item -Force $ChecksumFile }

$artifacts = Get-ChildItem -Path $OutputDir -File
$checksumLines = [System.Collections.Generic.List[string]]::new()
foreach ($file in $artifacts) {
    if ($file.Name -ne "SHA256SUMS.txt") {
        $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
        $line = "$hash  $($file.Name)"
        $checksumLines.Add($line)
        Write-Host "  $hash  $($file.Name)" -ForegroundColor Gray
    }
}
[System.IO.File]::WriteAllLines($ChecksumFile, $checksumLines.ToArray())

Write-Host "`n[SUCCESS] Build and Packaging Complete! Release artifacts ready in: $OutputDir" -ForegroundColor Green
return $Version
