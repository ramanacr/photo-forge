# PhotoForge Semantic Versioning Manager
# Manages automated version calculation, increments, and synchronized stamping across all platforms

function Get-LatestReleaseVersion {
    param(
        [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
    )

    $latestVersion = [version]"1.0.0"

    # 1. Check Git Tags (primary source of truth)
    try {
        $tags = git -C $RepoRoot tag -l "v*"
        $found = $false
        foreach ($t in $tags) {
            $cleaned = $t.Trim().TrimStart('v').TrimStart('V')
            if ($cleaned -match '^\d+(\.\d+)+$') {
                $parsed = [version]$cleaned
                if (-not $found -or $parsed -gt $latestVersion) {
                    $latestVersion = $parsed
                    $found = $true
                }
            }
        }
    } catch { }

    return $latestVersion.ToString()
}

function Get-NextVersion {
    param(
        [string]$CurrentVersion = (Get-LatestReleaseVersion),
        [ValidateSet("patch", "minor", "major", "auto")]
        [string]$Bump = "auto"
    )

    $v = [version]$CurrentVersion
    $major = [Math]::Max(0, $v.Major)
    $minor = [Math]::Max(0, $v.Minor)
    $patch = [Math]::Max(0, $v.Build)

    switch ($Bump) {
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "patch" {
            $patch++
        }
        "auto" {
            $patch++
        }
    }

    return "$major.$minor.$patch"
}

function Set-ProjectVersions {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version,
        [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
    )

    Write-Host "Stamping version $Version across all platform targets..." -ForegroundColor Cyan

    $vParts = $Version.Split('.')
    $maj = $vParts[0]
    $min = if ($vParts.Length -gt 1) { $vParts[1] } else { "0" }
    $bld = if ($vParts.Length -gt 2) { $vParts[2] } else { "0" }
    $asmVer = "$maj.$min.$bld.0"
    $versionCode = [int]$maj * 10000 + [int]$min * 100 + [int]$bld

    # 1. Update Directory.Build.props
    $propsPath = Join-Path $RepoRoot "Directory.Build.props"
    $propsContent = @"
<Project>
  <PropertyGroup>
    <Version>$Version</Version>
    <AssemblyVersion>$asmVer</AssemblyVersion>
    <FileVersion>$asmVer</FileVersion>
    <Product>PhotoForge</Product>
    <Company>PhotoForge Project</Company>
    <Authors>Ramana Reddy Chamakura</Authors>
    <Copyright>Copyright (c) 2026 PhotoForge Contributors</Copyright>
    <RepositoryUrl>https://github.com/ramanacr/photo-forge</RepositoryUrl>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>
</Project>
"@
    [System.IO.File]::WriteAllText($propsPath, $propsContent)
    Write-Host "  [OK] Directory.Build.props -> $Version" -ForegroundColor Green

    # 2. Update Installer Project & Assembly Name
    $installerProj = Join-Path $RepoRoot "apps\PhotoForge.Installer\PhotoForge.Installer.csproj"
    if (Test-Path $installerProj) {
        $content = [System.IO.File]::ReadAllText($installerProj)
        $content = $content -replace '<AssemblyName>PhotoForge-Setup-v[\d\.]+-x64</AssemblyName>', "<AssemblyName>PhotoForge-Setup-v$Version-x64</AssemblyName>"
        [System.IO.File]::WriteAllText($installerProj, $content)
        Write-Host "  [OK] PhotoForge.Installer.csproj -> PhotoForge-Setup-v$Version-x64" -ForegroundColor Green
    }

    # 3. Update Installer MainWindow Title
    $installerXAML = Join-Path $RepoRoot "apps\PhotoForge.Installer\MainWindow.xaml"
    if (Test-Path $installerXAML) {
        $content = [System.IO.File]::ReadAllText($installerXAML)
        $content = $content -replace 'Title="PhotoForge Setup v[\d\.]+"', "Title=`"PhotoForge Setup v$Version`""
        [System.IO.File]::WriteAllText($installerXAML, $content)
        Write-Host "  [OK] Installer MainWindow.xaml -> PhotoForge Setup v$Version" -ForegroundColor Green
    }

    # 4. Update Inno Setup Script
    $innoScript = Join-Path $RepoRoot "build\installer\photoforge.iss"
    if (Test-Path $innoScript) {
        $content = [System.IO.File]::ReadAllText($innoScript)
        $content = $content -replace '#define MyAppVersion "[\d\.]+"', "#define MyAppVersion `"$Version`""
        [System.IO.File]::WriteAllText($innoScript, $content)
        Write-Host "  [OK] build\installer\photoforge.iss -> $Version" -ForegroundColor Green
    }

    # 5. Update Android build.gradle.kts
    $androidGradle = Join-Path $RepoRoot "apps\PhotoForge.Android\build.gradle.kts"
    if (Test-Path $androidGradle) {
        $content = [System.IO.File]::ReadAllText($androidGradle)
        $content = $content -replace 'versionCode = \d+', "versionCode = $versionCode"
        $content = $content -replace 'versionName = "[\d\.]+"', "versionName = `"$Version`""
        [System.IO.File]::WriteAllText($androidGradle, $content)
        Write-Host "  [OK] apps\PhotoForge.Android\build.gradle.kts -> $Version (code: $versionCode)" -ForegroundColor Green
    }

    # 6. Update Android Settings & Main Layouts
    $settingsLayout = Join-Path $RepoRoot "apps\PhotoForge.Android\src\main\res\layout\activity_settings.xml"
    if (Test-Path $settingsLayout) {
        $content = [System.IO.File]::ReadAllText($settingsLayout)
        $content = $content -replace 'android:text="v[\d\.]+"', "android:text=`"v$Version`""
        [System.IO.File]::WriteAllText($settingsLayout, $content)
        Write-Host "  [OK] activity_settings.xml -> v$Version" -ForegroundColor Green
    }

    $mainLayout = Join-Path $RepoRoot "apps\PhotoForge.Android\src\main\res\layout\activity_main.xml"
    if (Test-Path $mainLayout) {
        $content = [System.IO.File]::ReadAllText($mainLayout)
        $content = $content -replace 'Offline Metadata Continuity Suite • v[\d\.]+', "Offline Metadata Continuity Suite • v$Version"
        [System.IO.File]::WriteAllText($mainLayout, $content)
        Write-Host "  [OK] activity_main.xml -> v$Version" -ForegroundColor Green
    }

    # 7. Update Website Landing and Downloads Pages
    $webIndex = Join-Path $RepoRoot "website\index.html"
    if (Test-Path $webIndex) {
        $content = [System.IO.File]::ReadAllText($webIndex)
        $content = $content -replace 'Version [\d\.]+ Released', "Version $Version Released"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-Setup-v[\d\.]+-x64\.exe', "v$Version/PhotoForge-Setup-v$Version-x64.exe"
        $content = $content -replace 'Windows Setup \(v[\d\.]+ x64\)', "Windows Setup (v$Version x64)"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-v[\d\.]+\.apk', "v$Version/PhotoForge-v$Version.apk"
        $content = $content -replace 'Android APK \(v[\d\.]+\)', "Android APK (v$Version)"
        [System.IO.File]::WriteAllText($webIndex, $content)
        Write-Host "  [OK] website\index.html -> v$Version" -ForegroundColor Green
    }

    $webDownloads = Join-Path $RepoRoot "website\downloads\index.html"
    if (Test-Path $webDownloads) {
        $content = [System.IO.File]::ReadAllText($webDownloads)
        $content = $content -replace 'PhotoForge v[\d\.]+ \(Latest Release\)', "PhotoForge v$Version (Latest Release)"
        $content = $content -replace '<span class="tag">v[\d\.]+</span>', "<span class=`"tag`">v$Version</span>"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-v[\d\.]+\.apk', "v$Version/PhotoForge-v$Version.apk"
        $content = $content -replace 'PhotoForge-v[\d\.]+\.apk', "PhotoForge-v$Version.apk"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-Setup-v[\d\.]+-x64\.exe', "v$Version/PhotoForge-Setup-v$Version-x64.exe"
        $content = $content -replace 'PhotoForge-Setup-v[\d\.]+-x64\.exe', "PhotoForge-Setup-v$Version-x64.exe"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-v[\d\.]+-Windows-x64\.zip', "v$Version/PhotoForge-v$Version-Windows-x64.zip"
        $content = $content -replace 'PhotoForge-v[\d\.]+-Windows-x64\.zip', "PhotoForge-v$Version-Windows-x64.zip"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-v[\d\.]+-Windows-arm64\.zip', "v$Version/PhotoForge-v$Version-Windows-arm64.zip"
        $content = $content -replace 'PhotoForge-v[\d\.]+-Windows-arm64\.zip', "PhotoForge-v$Version-Windows-arm64.zip"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-v[\d\.]+-CLI-win-x64\.zip', "v$Version/PhotoForge-v$Version-CLI-win-x64.zip"
        $content = $content -replace 'PhotoForge-v[\d\.]+-CLI-win-x64\.zip', "PhotoForge-v$Version-CLI-win-x64.zip"
        $content = $content -replace 'v[\d\.]+\/PhotoForge-v[\d\.]+-Android\.zip', "v$Version/PhotoForge-v$Version-Android.zip"
        $content = $content -replace 'PhotoForge-v[\d\.]+-Android\.zip', "PhotoForge-v$Version-Android.zip"
        $content = $content -replace 'v[\d\.]+\/SHA256SUMS\.txt', "v$Version/SHA256SUMS.txt"
        [System.IO.File]::WriteAllText($webDownloads, $content)
        Write-Host "  [OK] website\downloads\index.html -> v$Version" -ForegroundColor Green
    }
}
