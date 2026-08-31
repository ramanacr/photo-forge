package com.photoforge.app.model

import java.io.File
import java.io.Serializable
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

data class GpsCoordinate(
    val latitude: Double,
    val longitude: Double,
    val altitudeMeters: Double? = null,
    val speedKmH: Double? = null,
    val timestampUtc: Date? = null
) : Serializable {
    override fun toString(): String =
        if (altitudeMeters != null) "%.6f, %.6f (%.1fm)".format(Locale.US, latitude, longitude, altitudeMeters)
        else "%.6f, %.6f".format(Locale.US, latitude, longitude)
}

data class CameraInfo(
    val make: String? = null,
    val model: String? = null,
    val lensMake: String? = null,
    val lensModel: String? = null,
    val software: String? = null
) : Serializable

data class ExposureInfo(
    val iso: Int? = null,
    val exposureTimeSeconds: Double? = null,
    val fNumber: Double? = null,
    val focalLengthMm: Double? = null,
    val flash: String? = null,
    val whiteBalance: String? = null,
    val exposureBiasValue: Double? = null
) : Serializable

data class ExifData(
    val dateTimeOriginal: Date? = null,
    val createDate: Date? = null,
    val modifyDate: Date? = null,
    val camera: CameraInfo = CameraInfo(),
    val exposure: ExposureInfo = ExposureInfo(),
    val userComment: String? = null,
    val imageDescription: String? = null,
    val copyright: String? = null,
    val artist: String? = null,
    val additionalTags: Map<String, String> = emptyMap()
) : Serializable

data class IptcData(
    val title: String? = null,
    val caption: String? = null,
    val byline: String? = null,
    val copyrightNotice: String? = null,
    val credit: String? = null,
    val keywords: List<String> = emptyList(),
    val city: String? = null,
    val country: String? = null
) : Serializable

data class MigrationMarker(
    val processed: Boolean = true,
    val sourceFingerprint: String,
    val profile: String = "standard-v1",
    val migrationVersion: Int = 1,
    val engineVersion: String = "1.1.2",
    val processedAtUtc: Long = System.currentTimeMillis()
) : Serializable {
    fun toMarkerString(): String =
        "PF-MIG|v=$migrationVersion|src=$sourceFingerprint|prof=$profile|eng=$engineVersion|ts=$processedAtUtc"

    companion object {
        const val MARKER_PREFIX = "PF-MIG"

        fun tryParse(raw: String?): MigrationMarker? {
            if (raw.isNullOrBlank() || !raw.startsWith(MARKER_PREFIX, ignoreCase = true))
                return null

            return try {
                val parts = raw.split("|")
                val dict = mutableMapOf<String, String>()
                for (i in 1 until parts.size) {
                    val kv = parts[i].split("=", limit = 2)
                    if (kv.size == 2) dict[kv[0].lowercase(Locale.US)] = kv[1]
                }
                val src = dict["src"] ?: return null
                val v = dict["v"]?.toIntOrNull() ?: 1
                val prof = dict["prof"] ?: "standard-v1"
                val eng = dict["eng"] ?: "1.1.2"
                val ts = dict["ts"]?.toLongOrNull() ?: System.currentTimeMillis()

                MigrationMarker(
                    processed = true,
                    sourceFingerprint = src,
                    profile = prof,
                    migrationVersion = v,
                    engineVersion = eng,
                    processedAtUtc = ts
                )
            } catch (e: Exception) {
                null
            }
        }
    }
}

data class MetadataDocument(
    val exif: ExifData = ExifData(),
    val gps: GpsCoordinate? = null,
    val iptc: IptcData = IptcData(),
    val marker: MigrationMarker? = null,
    val format: String = "JPEG",
    val dimensions: Pair<Int, Int> = Pair(0, 0),
    val fileSizeBytes: Long = 0L,
    val sha256: String = ""
) : Serializable {
    val hasGps: Boolean get() = gps != null && (Math.abs(gps.latitude) > 0.000001 || Math.abs(gps.longitude) > 0.000001)
    val hasCaptureDate: Boolean get() = exif.dateTimeOriginal != null || exif.createDate != null
    val bestCaptureDate: Date? get() = exif.dateTimeOriginal ?: exif.createDate
    val cameraMakeAndModel: String?
        get() {
            val make = exif.camera.make?.trim()
            val model = exif.camera.model?.trim()
            return when {
                !make.isNullOrEmpty() && !model.isNullOrEmpty() -> "$make $model"
                !model.isNullOrEmpty() -> model
                !make.isNullOrEmpty() -> make
                else -> null
            }
        }
}

data class MetadataDiff(
    val copiedFromOriginal: MutableList<String> = mutableListOf(),
    val preservedFromTarget: MutableList<String> = mutableListOf(),
    val overwritten: MutableList<String> = mutableListOf(),
    val skipped: MutableList<String> = mutableListOf(),
    val warnings: MutableList<String> = mutableListOf()
) : Serializable {
    val hasWarnings: Boolean get() = warnings.isNotEmpty()
}

enum class GpsPrivacyPolicy(val title: String) {
    KEEP_EXACT("Keep Exact GPS"),
    ROUND("Round GPS (~1km Blur)"),
    REMOVE("Completely Strip GPS"),
    COPY_WITH_WARNING("Copy with Warning")
}

enum class ConversionQuality(val qualityInt: Int, val label: String) {
    LOSSLESS(100, "Lossless"),
    VERY_HIGH(95, "Very High (95%)"),
    HIGH(85, "High (85%)"),
    BALANCED(75, "Balanced (75%)"),
    SMALL(60, "Small (60%)")
}

enum class ConfidenceBand(val label: String) {
    AUTO_ACCEPT("Auto-Accept"),
    SUGGESTED("Suggested"),
    USER_REVIEW_REQUIRED("Review Required"),
    NO_MATCH("No Match");

    companion object {
        fun fromScore(score: Double): ConfidenceBand = when {
            score >= 0.95 -> AUTO_ACCEPT
            score >= 0.85 -> SUGGESTED
            score >= 0.70 -> USER_REVIEW_REQUIRED
            else -> NO_MATCH
        }
    }
}

data class SignalScores(
    val filenameScore: Double = 0.0,
    val timestampScore: Double = 0.0,
    val dimensionsScore: Double = 0.0,
    val metadataRemnantsScore: Double = 0.0,
    val perceptualSimilarityScore: Double = 0.0,
    val directoryRelationScore: Double = 0.0,
    val aggregateScore: Double = 0.0
) : Serializable

data class MatchingCandidate(
    val candidateFile: File,
    val candidateName: String,
    val score: Double,
    val band: ConfidenceBand,
    val signals: SignalScores,
    val reasons: List<String>,
    var perceptualHash: ULong? = null
) : Serializable

data class VerificationResult(
    val isValid: Boolean,
    val canBeReopened: Boolean,
    val hasValidDimensions: Boolean,
    val hasRequiredMetadata: Boolean,
    val hasMigrationMarker: Boolean,
    val verifiedFields: List<String> = emptyList(),
    val errors: List<String> = emptyList()
) : Serializable

enum class BatchItemStatus {
    SUCCESS,
    WARNING,
    SKIPPED,
    FAILED
}

data class BatchItemResult(
    val targetName: String,
    val originalName: String?,
    val status: BatchItemStatus,
    val outputPath: String?,
    val message: String
) : Serializable

data class BatchSummary(
    val totalItems: Int,
    val succeededCount: Int,
    val warningsCount: Int,
    val skippedCount: Int,
    val reviewRequiredCount: Int,
    val failedCount: Int,
    val durationMs: Long,
    val itemResults: List<BatchItemResult>
) : Serializable
