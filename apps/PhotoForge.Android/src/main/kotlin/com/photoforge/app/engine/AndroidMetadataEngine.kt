package com.photoforge.app.engine

import androidx.exifinterface.media.ExifInterface
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File

data class AndroidExifSummary(
    val make: String?,
    val model: String?,
    val dateTimeOriginal: String?,
    val latitude: Double?,
    val longitude: Double?,
    val altitude: Double?,
    val focalLength: Double?,
    val fNumber: Double?,
    val iso: Int?,
    val hasMigrationMarker: Boolean
)

class AndroidMetadataEngine {

    companion object {
        const val MARKER_PREFIX = "PF-MIG"
    }

    suspend fun extract(file: File): AndroidExifSummary = withContext(Dispatchers.IO) {
        val exif = ExifInterface(file.absolutePath)
        val latLong = FloatArray(2)
        val hasGps = exif.getLatLong(latLong)

        val userComment = exif.getAttribute(ExifInterface.TAG_USER_COMMENT) ?: ""
        val imageDesc = exif.getAttribute(ExifInterface.TAG_IMAGE_DESCRIPTION) ?: ""
        val hasMarker = userComment.contains(MARKER_PREFIX) || imageDesc.contains(MARKER_PREFIX)

        AndroidExifSummary(
            make = exif.getAttribute(ExifInterface.TAG_MAKE),
            model = exif.getAttribute(ExifInterface.TAG_MODEL),
            dateTimeOriginal = exif.getAttribute(ExifInterface.TAG_DATETIME_ORIGINAL),
            latitude = if (hasGps) latLong[0].toDouble() else null,
            longitude = if (hasGps) latLong[1].toDouble() else null,
            altitude = exif.getAltitude(0.0).takeIf { hasGps },
            focalLength = exif.getAttributeDouble(ExifInterface.TAG_FOCAL_LENGTH, 0.0).takeIf { it > 0 },
            fNumber = exif.getAttributeDouble(ExifInterface.TAG_F_NUMBER, 0.0).takeIf { it > 0 },
            iso = exif.getAttributeInt(ExifInterface.TAG_PHOTOGRAPHIC_SENSITIVITY, 0).takeIf { it > 0 },
            hasMigrationMarker = hasMarker
        )
    }

    suspend fun copyProvenance(
        originalFile: File,
        targetFile: File,
        sourceSha: String,
        roundGps: Boolean = false,
        stripGps: Boolean = false
    ) = withContext(Dispatchers.IO) {
        val origExif = ExifInterface(originalFile.absolutePath)
        val targetExif = ExifInterface(targetFile.absolutePath)

        // Copy camera provenance
        origExif.getAttribute(ExifInterface.TAG_MAKE)?.let { targetExif.setAttribute(ExifInterface.TAG_MAKE, it) }
        origExif.getAttribute(ExifInterface.TAG_MODEL)?.let { targetExif.setAttribute(ExifInterface.TAG_MODEL, it) }
        origExif.getAttribute(ExifInterface.TAG_DATETIME_ORIGINAL)?.let { targetExif.setAttribute(ExifInterface.TAG_DATETIME_ORIGINAL, it) }
        origExif.getAttribute(ExifInterface.TAG_FOCAL_LENGTH)?.let { targetExif.setAttribute(ExifInterface.TAG_FOCAL_LENGTH, it) }
        origExif.getAttribute(ExifInterface.TAG_F_NUMBER)?.let { targetExif.setAttribute(ExifInterface.TAG_F_NUMBER, it) }
        origExif.getAttribute(ExifInterface.TAG_PHOTOGRAPHIC_SENSITIVITY)?.let { targetExif.setAttribute(ExifInterface.TAG_PHOTOGRAPHIC_SENSITIVITY, it) }

        // GPS Handling
        if (!stripGps) {
            val latLong = FloatArray(2)
            if (origExif.getLatLong(latLong)) {
                var lat = latLong[0].toDouble()
                var lon = latLong[1].toDouble()
                if (roundGps) {
                    lat = Math.round(lat * 100.0) / 100.0
                    lon = Math.round(lon * 100.0) / 100.0
                }
                targetExif.setLatLong(lat, lon)
            }
        }

        // Migration Marker Injection
        val marker = "PF-MIG|v=1|src=$sourceSha|prof=standard-v1|eng=1.0.0|ts=${System.currentTimeMillis()}"
        targetExif.setAttribute(ExifInterface.TAG_USER_COMMENT, marker)
        targetExif.setAttribute(ExifInterface.TAG_IMAGE_DESCRIPTION, marker)

        targetExif.saveAttributes()
    }
}
