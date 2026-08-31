package com.photoforge.app.engine

import androidx.exifinterface.media.ExifInterface
import com.drew.imaging.ImageMetadataReader
import com.drew.metadata.exif.ExifIFD0Directory
import com.drew.metadata.exif.ExifSubIFDDirectory
import com.drew.metadata.exif.GpsDirectory
import com.drew.metadata.iptc.IptcDirectory
import com.photoforge.app.model.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileInputStream
import java.security.MessageDigest
import java.text.SimpleDateFormat
import java.util.*

class AndroidMetadataEngine(private val imageEngine: AndroidImageEngine = AndroidImageEngine()) {

    private val exifDateFormat = SimpleDateFormat("yyyy:MM:dd HH:mm:ss", Locale.US).apply {
        timeZone = TimeZone.getTimeZone("UTC")
    }

    suspend fun extractDocument(file: File): MetadataDocument = withContext(Dispatchers.IO) {
        if (!file.exists()) return@withContext MetadataDocument()

        val format = imageEngine.sniffFormat(file)
        val dims = imageEngine.inspectDimensions(file)
        val size = file.length()
        val sha = computeSha256(file)

        var exifData = ExifData()
        var gpsData: GpsCoordinate? = null
        var iptcData = IptcData()
        var marker: MigrationMarker? = null
        val rawTags = mutableMapOf<String, String>()

        // 1. Drew Noakes extraction for broad format coverage
        try {
            val metadata = ImageMetadataReader.readMetadata(file)

            val ifd0 = metadata.getFirstDirectoryOfType(ExifIFD0Directory::class.java)
            val subIfd = metadata.getFirstDirectoryOfType(ExifSubIFDDirectory::class.java)
            val gpsDir = metadata.getFirstDirectoryOfType(GpsDirectory::class.java)
            val iptcDir = metadata.getFirstDirectoryOfType(IptcDirectory::class.java)

            val camera = CameraInfo(
                make = ifd0?.getString(ExifIFD0Directory.TAG_MAKE)?.trim(),
                model = ifd0?.getString(ExifIFD0Directory.TAG_MODEL)?.trim(),
                lensMake = subIfd?.getString(ExifSubIFDDirectory.TAG_LENS_MAKE)?.trim(),
                lensModel = subIfd?.getString(ExifSubIFDDirectory.TAG_LENS_MODEL)?.trim(),
                software = ifd0?.getString(ExifIFD0Directory.TAG_SOFTWARE)?.trim()
            )

            val iso = subIfd?.getInt(ExifSubIFDDirectory.TAG_ISO_EQUIVALENT)
            val fNumber = subIfd?.getDoubleObject(ExifSubIFDDirectory.TAG_FNUMBER)
            val focal = subIfd?.getDoubleObject(ExifSubIFDDirectory.TAG_FOCAL_LENGTH)
            val exposureTime = subIfd?.getDoubleObject(ExifSubIFDDirectory.TAG_EXPOSURE_TIME)

            val exposure = ExposureInfo(
                iso = iso,
                exposureTimeSeconds = exposureTime,
                fNumber = fNumber,
                focalLengthMm = focal
            )

            val dateOrig = subIfd?.getDate(ExifSubIFDDirectory.TAG_DATETIME_ORIGINAL)
                ?: ifd0?.getDate(ExifIFD0Directory.TAG_DATETIME)
            val dateDigitized = subIfd?.getDate(ExifSubIFDDirectory.TAG_DATETIME_DIGITIZED)

            val userComment = subIfd?.getString(ExifSubIFDDirectory.TAG_USER_COMMENT)
            val imageDesc = ifd0?.getString(ExifIFD0Directory.TAG_IMAGE_DESCRIPTION)
            val artist = ifd0?.getString(ExifIFD0Directory.TAG_ARTIST)
            val copyright = ifd0?.getString(ExifIFD0Directory.TAG_COPYRIGHT)

            exifData = ExifData(
                dateTimeOriginal = dateOrig,
                createDate = dateDigitized,
                camera = camera,
                exposure = exposure,
                userComment = userComment,
                imageDescription = imageDesc,
                artist = artist,
                copyright = copyright
            )

            // GPS
            val geoLocation = gpsDir?.geoLocation
            if (geoLocation != null) {
                val alt = gpsDir.getDoubleObject(GpsDirectory.TAG_ALTITUDE)
                gpsData = GpsCoordinate(
                    latitude = geoLocation.latitude,
                    longitude = geoLocation.longitude,
                    altitudeMeters = alt
                )
            }

            // IPTC
            if (iptcDir != null) {
                val keywords = iptcDir.getStringArray(IptcDirectory.TAG_KEYWORDS)?.toList() ?: emptyList()
                iptcData = IptcData(
                    title = iptcDir.getString(IptcDirectory.TAG_OBJECT_NAME),
                    caption = iptcDir.getString(IptcDirectory.TAG_CAPTION),
                    byline = iptcDir.getString(IptcDirectory.TAG_BY_LINE),
                    copyrightNotice = iptcDir.getString(IptcDirectory.TAG_COPYRIGHT_NOTICE),
                    credit = iptcDir.getString(IptcDirectory.TAG_CREDIT),
                    keywords = keywords,
                    city = iptcDir.getString(IptcDirectory.TAG_CITY),
                    country = iptcDir.getString(IptcDirectory.TAG_COUNTRY_OR_PRIMARY_LOCATION_NAME)
                )
            }

            // Populate all raw tags
            for (dir in metadata.directories) {
                for (tag in dir.tags) {
                    rawTags["${dir.name} : ${tag.tagName}"] = tag.description ?: ""
                }
            }
        } catch (e: Exception) {
            // fallback to Android ExifInterface
        }

        // 2. Supplement with Android ExifInterface if needed
        try {
            val exif = ExifInterface(file.absolutePath)
            val latLong = FloatArray(2)
            if (gpsData == null && exif.getLatLong(latLong)) {
                gpsData = GpsCoordinate(
                    latitude = latLong[0].toDouble(),
                    longitude = latLong[1].toDouble(),
                    altitudeMeters = exif.getAltitude(0.0)
                )
            }

            val userComment = exif.getAttribute(ExifInterface.TAG_USER_COMMENT) ?: exifData.userComment
            val imageDesc = exif.getAttribute(ExifInterface.TAG_IMAGE_DESCRIPTION) ?: exifData.imageDescription

            marker = MigrationMarker.tryParse(userComment) ?: MigrationMarker.tryParse(imageDesc)

            if (exifData.dateTimeOriginal == null) {
                val dtStr = exif.getAttribute(ExifInterface.TAG_DATETIME_ORIGINAL)
                if (!dtStr.isNullOrBlank()) {
                    try { exifData = exifData.copy(dateTimeOriginal = exifDateFormat.parse(dtStr)) } catch (e: Exception) {}
                }
            }
        } catch (e: Exception) {
            // ignore
        }

        MetadataDocument(
            exif = exifData,
            gps = gpsData,
            iptc = iptcData,
            marker = marker,
            format = format,
            dimensions = dims,
            fileSizeBytes = size,
            sha256 = sha
        )
    }

    fun computeDiff(
        original: MetadataDocument,
        target: MetadataDocument,
        policy: GpsPrivacyPolicy = GpsPrivacyPolicy.KEEP_EXACT
    ): MetadataDiff {
        val diff = MetadataDiff()

        // EXIF Camera
        if (!original.exif.camera.make.isNullOrBlank()) {
            diff.copiedFromOriginal.add("Make: ${original.exif.camera.make}")
        }
        if (!original.exif.camera.model.isNullOrBlank()) {
            diff.copiedFromOriginal.add("Model: ${original.exif.camera.model}")
        }
        if (!original.exif.camera.lensModel.isNullOrBlank()) {
            diff.copiedFromOriginal.add("Lens: ${original.exif.camera.lensModel}")
        }

        // EXIF Exposure
        if (original.exif.dateTimeOriginal != null) {
            diff.copiedFromOriginal.add("DateTimeOriginal: ${exifDateFormat.format(original.exif.dateTimeOriginal)}")
        }
        if (original.exif.exposure.iso != null) {
            diff.copiedFromOriginal.add("ISO: ${original.exif.exposure.iso}")
        }
        if (original.exif.exposure.fNumber != null) {
            diff.copiedFromOriginal.add("Aperture: f/${original.exif.exposure.fNumber}")
        }
        if (original.exif.exposure.focalLengthMm != null) {
            diff.copiedFromOriginal.add("FocalLength: ${original.exif.exposure.focalLengthMm}mm")
        }

        // GPS Handling Diff
        if (original.hasGps) {
            when (policy) {
                GpsPrivacyPolicy.REMOVE -> {
                    diff.skipped.add("GPS Location (Stripped per privacy policy)")
                }
                GpsPrivacyPolicy.ROUND -> {
                    val lat = Math.round((original.gps?.latitude ?: 0.0) * 100.0) / 100.0
                    val lon = Math.round((original.gps?.longitude ?: 0.0) * 100.0) / 100.0
                    diff.copiedFromOriginal.add("GPS Location: $lat, $lon (Obscured ~1km)")
                }
                GpsPrivacyPolicy.COPY_WITH_WARNING -> {
                    diff.copiedFromOriginal.add("GPS Location: ${original.gps}")
                    diff.warnings.add("Full precision GPS location was transferred to target image.")
                }
                GpsPrivacyPolicy.KEEP_EXACT -> {
                    diff.copiedFromOriginal.add("GPS Location: ${original.gps}")
                }
            }
        }

        // Preserved from target
        if (target.iptc.keywords.isNotEmpty()) {
            diff.preservedFromTarget.add("Keywords (${target.iptc.keywords.size}): ${target.iptc.keywords.joinToString()}")
        }
        if (!target.iptc.caption.isNullOrBlank()) {
            diff.preservedFromTarget.add("Caption: ${target.iptc.caption}")
        }

        // Idempotency Marker
        diff.copiedFromOriginal.add("PhotoForge Marker (PF-MIG Provenance Tag)")

        return diff
    }

    suspend fun copyProvenance(
        originalFile: File,
        targetFile: File,
        sourceSha: String,
        policy: GpsPrivacyPolicy = GpsPrivacyPolicy.KEEP_EXACT,
        profileName: String = "standard-v1"
    ): MetadataDiff = withContext(Dispatchers.IO) {
        val origDoc = extractDocument(originalFile)
        val targetDoc = extractDocument(targetFile)
        val diff = computeDiff(origDoc, targetDoc, policy)

        val origExif = ExifInterface(originalFile.absolutePath)
        val targetExif = ExifInterface(targetFile.absolutePath)

        // 1. Copy Camera info
        origExif.getAttribute(ExifInterface.TAG_MAKE)?.let { targetExif.setAttribute(ExifInterface.TAG_MAKE, it) }
        origExif.getAttribute(ExifInterface.TAG_MODEL)?.let { targetExif.setAttribute(ExifInterface.TAG_MODEL, it) }
        origExif.getAttribute(ExifInterface.TAG_DATETIME_ORIGINAL)?.let { targetExif.setAttribute(ExifInterface.TAG_DATETIME_ORIGINAL, it) }
        origExif.getAttribute(ExifInterface.TAG_DATETIME_DIGITIZED)?.let { targetExif.setAttribute(ExifInterface.TAG_DATETIME_DIGITIZED, it) }
        origExif.getAttribute(ExifInterface.TAG_FOCAL_LENGTH)?.let { targetExif.setAttribute(ExifInterface.TAG_FOCAL_LENGTH, it) }
        origExif.getAttribute(ExifInterface.TAG_F_NUMBER)?.let { targetExif.setAttribute(ExifInterface.TAG_F_NUMBER, it) }
        origExif.getAttribute(ExifInterface.TAG_PHOTOGRAPHIC_SENSITIVITY)?.let { targetExif.setAttribute(ExifInterface.TAG_PHOTOGRAPHIC_SENSITIVITY, it) }
        origExif.getAttribute(ExifInterface.TAG_EXPOSURE_TIME)?.let { targetExif.setAttribute(ExifInterface.TAG_EXPOSURE_TIME, it) }
        origExif.getAttribute(ExifInterface.TAG_WHITE_BALANCE)?.let { targetExif.setAttribute(ExifInterface.TAG_WHITE_BALANCE, it) }
        origExif.getAttribute(ExifInterface.TAG_FLASH)?.let { targetExif.setAttribute(ExifInterface.TAG_FLASH, it) }
        origExif.getAttribute(ExifInterface.TAG_ARTIST)?.let { targetExif.setAttribute(ExifInterface.TAG_ARTIST, it) }
        origExif.getAttribute(ExifInterface.TAG_COPYRIGHT)?.let { targetExif.setAttribute(ExifInterface.TAG_COPYRIGHT, it) }

        // 2. Handle GPS Privacy Policy
        if (policy != GpsPrivacyPolicy.REMOVE) {
            val latLong = FloatArray(2)
            if (origExif.getLatLong(latLong)) {
                var lat = latLong[0].toDouble()
                var lon = latLong[1].toDouble()
                if (policy == GpsPrivacyPolicy.ROUND) {
                    lat = Math.round(lat * 100.0) / 100.0
                    lon = Math.round(lon * 100.0) / 100.0
                }
                targetExif.setLatLong(lat, lon)
                origExif.getAltitude(0.0).let { if (it > 0) targetExif.setAltitude(it) }
            }
        }

        // 3. Inject Idempotency Migration Marker
        val marker = MigrationMarker(
            processed = true,
            sourceFingerprint = sourceSha,
            profile = profileName,
            migrationVersion = 1,
            engineVersion = "1.1.2",
            processedAtUtc = System.currentTimeMillis()
        )
        val markerStr = marker.toMarkerString()
        targetExif.setAttribute(ExifInterface.TAG_USER_COMMENT, markerStr)
        targetExif.setAttribute(ExifInterface.TAG_IMAGE_DESCRIPTION, markerStr)

        targetExif.saveAttributes()
        diff
    }

    private fun computeSha256(file: File): String {
        return try {
            val digest = MessageDigest.getInstance("SHA-256")
            file.inputStream().use { input ->
                val buffer = ByteArray(8192)
                var read: Int
                while (input.read(buffer).also { read = it } > 0) {
                    digest.update(buffer, 0, read)
                }
            }
            digest.digest().joinToString("") { "%02x".format(it) }
        } catch (e: Exception) {
            ""
        }
    }
}
