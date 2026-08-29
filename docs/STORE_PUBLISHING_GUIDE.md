# 🏪 PhotoForge Store Publishing & Distribution Guide

Comprehensive, step-by-step instructions for publishing **PhotoForge** to the **Google Play Store** (Android) and the **Microsoft Store** (Windows).

---

## 📑 Table of Contents

1. [Branding & Asset Inventory Reference](#1-branding--asset-inventory-reference)
2. [Google Play Store Publishing Guide (Android)](#2-google-play-store-publishing-guide-android)
   - [2.1 Prerequisites & Developer Account](#21-prerequisites--developer-account)
   - [2.2 Generating Release Keystore & Signing](#22-generating-release-keystore--signing)
   - [2.3 Building the Android App Bundle (.aab)](#23-building-the-android-app-bundle-aab)
   - [2.4 Store Listing & Graphic Assets](#24-store-listing--graphic-assets)
   - [2.5 Content Rating, Privacy & Policy Declarations](#25-content-rating-privacy--policy-declarations)
   - [2.6 Release Tracks & Rollout](#26-release-tracks--rollout)
3. [Microsoft Store Publishing Guide (Windows)](#3-microsoft-store-publishing-guide-windows)
   - [3.1 Prerequisites & Microsoft Partner Center](#31-prerequisites--microsoft-partner-center)
   - [3.2 Choosing Submission Type (MSIX vs. Standalone Win32 Installer)](#32-choosing-submission-type-msix-vs-standalone-win32-installer)
   - [3.3 Packaging as MSIX / Windows App Package](#33-packaging-as-msix--windows-app-package)
   - [3.4 Submitting Standalone Setup Installer (Win32)](#34-submitting-standalone-setup-installer-win32)
   - [3.5 Store Listing, Product Details & Age Ratings](#35-store-listing-product-details--age-ratings)
   - [3.6 Submission & Certification](#36-submission--certification)
4. [Continuous Delivery & Automated Store Releases](#4-continuous-delivery--automated-store-releases)

---

## 1. Branding & Asset Inventory Reference

All required store graphics, banners, and high-resolution icons are pre-generated and located in `docs/branding/`:

| Store Requirement | Dimensions | Repository File Path |
|---|---|---|
| **Google Play App Icon** | 512 × 512 px (PNG) | [`docs/branding/icons/android-playstore-512.png`](file:///d:/Practice/photo-forge/docs/branding/icons/android-playstore-512.png) |
| **Google Play Feature Graphic** | 1024 × 500 px (JPG/PNG) | [`docs/branding/photoforge_feature_graphic.jpg`](file:///d:/Practice/photo-forge/docs/branding/photoforge_feature_graphic.jpg) |
| **Microsoft Store App Logo** | 300 × 300 px (PNG) | [`docs/branding/icons/icon-512x512.png`](file:///d:/Practice/photo-forge/docs/branding/icons/icon-512x512.png) |
| **Microsoft Store Small Tile** | 71 × 71 px & 150 × 150 px | [`docs/branding/icons/icon-128x128.png`](file:///d:/Practice/photo-forge/docs/branding/icons/icon-128x128.png) |
| **Microsoft Store Hero Poster** | 1920 × 1080 px (16:9) | [`docs/branding/photoforge_banner.jpg`](file:///d:/Practice/photo-forge/docs/branding/photoforge_banner.jpg) |
| **Windows Executable Icon** | Multi-layer .ICO | [`docs/branding/icons/app.ico`](file:///d:/Practice/photo-forge/docs/branding/icons/app.ico) |

---

## 2. Google Play Store Publishing Guide (Android)

Google Play mandates **Android App Bundles (`.aab`)** for all new apps.

### 2.1 Prerequisites & Developer Account
1. Create a Google Play Developer Account at [play.google.com/console/signup](https://play.google.com/console/signup) (one-time $25 USD fee).
2. Complete Identity Verification and Dun & Bradstreet (D-U-N-S) organization verification if registering as an organization.

### 2.2 Generating Release Keystore & Signing
Generate a production signing keystore using the standard Java `keytool` utility:

```powershell
# Open terminal in apps/PhotoForge.Android
keytool -genkeypair -v `
  -keystore photoforge-release.jks `
  -alias photoforge-key `
  -keyalg RSA `
  -keysize 2048 `
  -validity 10000 `
  -storepass "YourStrongPasswordHere" `
  -keypass "YourStrongPasswordHere" `
  -dname "CN=PhotoForge, OU=Mobile, O=PhotoForge, L=City, ST=State, C=US"
```

> [!WARNING]
> Back up `photoforge-release.jks` securely in a password manager or secure vault. Losing this keystore prevents publishing app updates on Google Play.

Configure signing in `apps/PhotoForge.Android/build.gradle.kts`:
```kotlin
android {
    signingConfigs {
        create("release") {
            storeFile = file("photoforge-release.jks")
            storePassword = System.getenv("KEYSTORE_PASSWORD") ?: "YourStrongPasswordHere"
            keyAlias = "photoforge-key"
            keyPassword = System.getenv("KEY_PASSWORD") ?: "YourStrongPasswordHere"
        }
    }
    buildTypes {
        release {
            isMinifyEnabled = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = signingConfigs.getByName("release")
        }
    }
}
```

### 2.3 Building the Android App Bundle (.aab)
To build the signed release bundle:

```powershell
cd apps/PhotoForge.Android
./gradlew bundleRelease
```
The output file will be generated at:
`apps/PhotoForge.Android/build/outputs/bundle/release/PhotoForge.Android-release.aab`

---

### 2.4 Store Listing & Graphic Assets

Navigate to **Google Play Console** → Select App → **Grow** → **Store presence** → **Main store listing**:

1. **App Name:** `PhotoForge: Photo Metadata & HEIC` *(max 30 characters)*
2. **Short Description:** `Restore lost EXIF, GPS & exposure data in edited photos. 100% offline.` *(max 80 characters)*
3. **Full Description:**
   ```text
   When photos are edited in third-party photo editors or shared across social networks, crucial capture provenance and EXIF metadata is routinely stripped.

   PhotoForge restores capture metadata by linking edited photos back to original camera captures using deterministic multi-signal candidate matching.

   Key Features:
   • Lossless Metadata Restoration: Reintegrates camera make/model, shutter speed, ISO, aperture, and subsecond timestamps.
   • GPS Privacy Controls: Choose exact location restoration, 1km radius privacy fuzzing, or complete location removal.
   • HEIC & WebP Modern Format Studio: Convert photos with up to 50% storage savings while preserving 100% metadata continuity.
   • 100% Offline & Private: Zero internet permissions requested. Zero telemetry. All processing happens on-device.
   ```
4. **App Icon:** Upload `docs/branding/icons/android-playstore-512.png` (512×512, 32-bit PNG).
5. **Feature Graphic:** Upload `docs/branding/photoforge_feature_graphic.jpg` (1024×500, JPG).
6. **Phone Screenshots:** Upload at least 4 screenshots (16:9 or 18:9 aspect ratio, min 1080px).

---

### 2.5 Content Rating, Privacy & Policy Declarations

Navigate to **Policy and Programs** → **App Content**:

1. **Privacy Policy:** Provide URL to your privacy policy (e.g. `https://github.com/ramanacr/photo-forge/blob/main/docs/PRIVACY.md`).
2. **Data Safety Form:**
   - Does your app collect or share user data? **No**
   - Are any network calls made? **No**
3. **Permissions Declaration:**
   - PhotoForge operates strictly with Scoped Storage / Storage Access Framework (`READ_MEDIA_IMAGES` or `ACTION_OPEN_DOCUMENT`).
   - Notice: **`android.permission.INTERNET` is not declared**, proving 100% offline safety to Google reviewers.
4. **Content Rating (IARC):** Complete questionnaire (Category: Utility/Photo tool → Rating: Everyone / PEGI 3).

---

### 2.6 Release Tracks & Rollout

1. Go to **Testing** → **Internal testing** → Create new release.
2. Upload `PhotoForge.Android-release.aab`.
3. Add internal tester email addresses.
4. Verify installation on physical Android devices.
5. Promote from **Internal Testing** → **Production Release** → **Start rollout to Production**.

---

## 3. Microsoft Store Publishing Guide (Windows)

Microsoft Partner Center allows distributing Windows apps either as **MSIX packages** or directly as **Win32 Setup Installers (`.exe`)**.

### 3.1 Prerequisites & Microsoft Partner Center
1. Register for a **Microsoft Partner Center** developer account at [partner.microsoft.com/dashboard/registration](https://partner.microsoft.com/dashboard/registration) (one-time $19 USD fee for individual, $99 USD for company).
2. Reserve your application product name: **`PhotoForge`**.

---

### 3.2 Choosing Submission Type (MSIX vs. Standalone Win32 Installer)

| Feature | MSIX Windows Package | Win32 Standalone Installer (.exe) |
|---|---|---|
| **Installation Format** | `.msix` / `.msixupload` | `PhotoForge-Setup-v1.1.0-x64.exe` |
| **Store Ingestion** | Hosted & updated directly via Microsoft CDN | Store downloads your installer or links directly to GitHub Release |
| **Silent Updates** | Automatic background updates via Store engine | Handled via PhotoForge built-in GitHub update service |
| **System Access** | Containerized / Full Trust capability | Native full system access / Explorer context menu |
| **Recommended For** | Windows Store users seeking 1-click install | Universal Windows installation |

---

### 3.3 Packaging as MSIX / Windows App Package

To generate an `.msix` package:

1. Create a `PhotoForge.Package.wapproj` (Windows Application Packaging Project) referencing `PhotoForge.Desktop.csproj`.
2. Configure `Package.appxmanifest`:
   ```xml
   <Identity Name="PhotoForge.App"
             Publisher="CN=YOUR_PARTNER_CENTER_PUBLISHER_ID"
             Version="1.1.0.0"
             ProcessorArchitecture="x64" />
   <Properties>
     <DisplayName>PhotoForge</DisplayName>
     <PublisherDisplayName>PhotoForge Team</PublisherDisplayName>
     <Logo>Images\StoreLogo.png</Logo>
   </Properties>
   <Capabilities>
     <rescap:Capability Name="runFullTrust" />
   </Capabilities>
   ```
3. Build the MSIX package:
   ```powershell
   dotnet publish apps/PhotoForge.Desktop/PhotoForge.Desktop.csproj `
     -c Release `
     -r win-x64 `
     -p:GenerateAppInstallerFile=true `
     -p:AppxPackageDir="build/dist/msix/"
   ```

---

### 3.4 Submitting Standalone Setup Installer (Win32)

Microsoft Partner Center officially supports direct submission of standard Win32 setup installers:

1. In Partner Center, create a new submission for **PhotoForge**.
2. Under **Packages**, select **"I want to provide an installer (.exe, .msi)"**.
3. **Installer Download URL:** Provide the direct URL from the GitHub Release:
   `https://github.com/ramanacr/photo-forge/releases/download/v1.1.0/PhotoForge-Setup-v1.1.0-x64.exe`
4. **Installer Parameters:**
   - Silent install switch: `--silent`
   - Silent uninstall switch: `--silent`
5. **Architecture:** Check `x64` and `ARM64`.

---

### 3.5 Store Listing, Product Details & Age Ratings

1. **Store Product Title:** `PhotoForge`
2. **Short Description:** `Offline-first photo metadata continuity & HEIC/WebP format conversion platform.`
3. **Keywords / Search Tags:** `photo metadata`, `exif restore`, `gps privacy`, `heic converter`, `photoforge`, `raw photo`
4. **Visual Assets:**
   - App Tile Icon (300×300): Upload `docs/branding/icons/icon-512x512.png`.
   - Store Logo (50×50): Upload `docs/branding/icons/icon-64x64.png`.
   - Hero Banner (1920×1080): Upload `docs/branding/photoforge_banner.jpg`.
   - Feature Graphic: Upload `docs/branding/photoforge_feature_graphic.jpg`.
   - Screenshots: Desktop app UI screenshots (1920×1080 or 1366×768).
5. **Age Rating:** Complete the IARC questionnaire (General / Utility → Rated 3+).

---

### 3.6 Submission & Certification

1. Click **Submit to the Store**.
2. Microsoft's automated certification pipeline tests:
   - Clean silent install and uninstall without dangling processes.
   - Malware & signature verification.
   - No crash-on-launch.
3. Upon approval (typically 24–48 hours), PhotoForge becomes available worldwide in the Microsoft Store.

---

## 4. Continuous Delivery & Automated Store Releases

To generate all store distribution packages in one command, run our automated store packaging script:

```powershell
# Build all release packages & store bundles
pwsh -File .\build\publish-release.ps1 -Bump patch
```

### Store Release Checklist:
- [ ] Increment version with `publish-release.ps1` (`v1.1.0` → `v1.1.1` or `v1.2.0`).
- [ ] Upload `.aab` to Google Play Console (Production / Beta track).
- [ ] Update installer URL or upload `.msix` to Microsoft Partner Center.
- [ ] Verify `SHA256SUMS.txt` checksums match release binaries.
- [ ] Submit for certification.
