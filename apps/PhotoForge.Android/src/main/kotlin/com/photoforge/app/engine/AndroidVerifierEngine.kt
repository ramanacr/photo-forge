package com.photoforge.app.engine

import android.graphics.BitmapFactory
import com.photoforge.app.model.VerificationResult
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File

class AndroidVerifierEngine(
    private val metadataEngine: AndroidMetadataEngine = AndroidMetadataEngine(),
    private val imageEngine: AndroidImageEngine = AndroidImageEngine()
) {

    suspend fun verify(file: File): VerificationResult = withContext(Dispatchers.IO) {
        val errors = mutableListOf<String>()
        val verifiedFields = mutableListOf<String>()

        if (!file.exists() || file.length() == 0L) {
            return@withContext VerificationResult(
                isValid = false,
                canBeReopened = false,
                hasValidDimensions = false,
                hasRequiredMetadata = false,
                hasMigrationMarker = false,
                errors = listOf("File does not exist or has zero length.")
            )
        }

        // 1. Check if can be reopened
        var canReopen = false
        var validDims = false
        var width = 0
        var height = 0

        try {
            val options = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            BitmapFactory.decodeFile(file.absolutePath, options)
            width = options.outWidth
            height = options.outHeight

            if (width > 0 && height > 0) {
                validDims = true
                verifiedFields.add("Valid Image Dimensions: ${width}x${height}px")
            } else {
                errors.add("Invalid or zero image dimensions detected.")
            }

            // Quick decode sample to ensure stream integrity
            val sampleOptions = BitmapFactory.Options().apply { inSampleSize = 8 }
            val bmp = BitmapFactory.decodeFile(file.absolutePath, sampleOptions)
            if (bmp != null) {
                canReopen = true
                verifiedFields.add("Image Stream Decodable (Bitmap verification passed)")
                bmp.recycle()
            } else {
                errors.add("Failed to decode bitmap bytes from file.")
            }
        } catch (e: Exception) {
            errors.add("Bitmap decode exception: ${e.message}")
        }

        // 2. Check metadata
        val doc = metadataEngine.extractDocument(file)
        var hasMeta = false

        if (doc.cameraMakeAndModel != null) {
            hasMeta = true
            verifiedFields.add("Camera Equipment: ${doc.cameraMakeAndModel}")
        }
        if (doc.exif.dateTimeOriginal != null) {
            hasMeta = true
            verifiedFields.add("Capture Timestamp: ${doc.exif.dateTimeOriginal}")
        }
        if (doc.hasGps) {
            hasMeta = true
            verifiedFields.add("GPS Location: ${doc.gps}")
        }
        if (doc.exif.exposure.iso != null) {
            hasMeta = true
            verifiedFields.add("Exposure: ISO ${doc.exif.exposure.iso}, f/${doc.exif.exposure.fNumber ?: "?"}")
        }

        // 3. Check migration marker
        var hasMarker = false
        if (doc.marker != null) {
            hasMarker = true
            verifiedFields.add("PhotoForge Marker: Valid (src=${doc.marker.sourceFingerprint.take(12)}..., v=${doc.marker.migrationVersion})")
        }

        val isValid = canReopen && validDims && errors.isEmpty()

        VerificationResult(
            isValid = isValid,
            canBeReopened = canReopen,
            hasValidDimensions = validDims,
            hasRequiredMetadata = hasMeta,
            hasMigrationMarker = hasMarker,
            verifiedFields = verifiedFields,
            errors = errors
        )
    }
}
