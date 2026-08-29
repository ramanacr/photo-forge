# PhotoForge CLI Reference Manual

The `photoforge` command-line utility provides deterministic metadata restoration, HEIC conversion, and batch automation.

## Command Overview

```powershell
photoforge <command> [options]
```

### 1. `restore`
Restores original camera provenance and capture metadata to an edited target image.

```powershell
# Basic single pair restoration
photoforge restore --original "C:\Photos\Originals\IMG_001.jpg" --edited "C:\Photos\Edited\IMG_001_edited.jpg"

# Custom output destination
photoforge restore --original "C:\Photos\IMG_001.jpg" --edited "C:\Photos\IMG_001_edit.jpg" --output "C:\Photos\Restored\IMG_001.jpg"

# Privacy options for GPS
photoforge restore --original "IMG_001.jpg" --edited "IMG_001_edit.jpg" --gps remove
photoforge restore --original "IMG_001.jpg" --edited "IMG_001_edit.jpg" --gps round

# Dry-run preview
photoforge restore --original "IMG_001.jpg" --edited "IMG_001_edit.jpg" --dry-run

# Machine-readable JSON output
photoforge restore --original "IMG_001.jpg" --edited "IMG_001_edit.jpg" --json
```

### 2. `convert`
Converts images to HEIC or WebP while preserving full metadata continuity.

```powershell
photoforge convert --input "IMG_001.jpg" --format heic --quality high
photoforge convert --input "IMG_002.png" --format webp --quality lossless
```

### 3. `verify`
Independently validates written files, confirms EXIF/GPS preservation, checks dimension consistency, and verifies migration markers.

```powershell
photoforge verify --input "C:\Photos\Restored\IMG_001.jpg"
photoforge verify --input "C:\Photos\Restored\IMG_001.jpg" --json
```

### 4. `inspect`
Extracts and displays EXIF, GPS, IPTC, XMP, ICC, and migration marker tags.

```powershell
photoforge inspect --input "IMG_001.jpg"
photoforge inspect --input "IMG_001.jpg" --json
```

### 5. `match`
Finds and ranks original candidates from a folder for a given edited photo.

```powershell
photoforge match --edited "IMG_001_edit.jpg" --originals "C:\Photos\Originals"
```

### 6. `batch`
Batch restores an entire folder of edited photos against an originals repository.

```powershell
photoforge batch --input "C:\Photos\Edited" --originals "C:\Photos\Originals" --output "C:\Photos\Restored" --auto-accept
```

### 7. Shell Context Menu Integration
```powershell
photoforge --register-shell
photoforge --unregister-shell
```
