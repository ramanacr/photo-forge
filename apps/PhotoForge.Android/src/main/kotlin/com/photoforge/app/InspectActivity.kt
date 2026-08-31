package com.photoforge.app

import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.photoforge.app.databinding.ActivityInspectBinding
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.storage.AndroidStorageBridge
import kotlinx.coroutines.launch
import java.io.File
import java.text.SimpleDateFormat
import java.util.Locale

class InspectActivity : AppCompatActivity() {

    private lateinit var binding: ActivityInspectBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private val metadataEngine = AndroidMetadataEngine()

    private val pickPhotoLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let { inspectUri(it) }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityInspectBinding.inflate(layoutInflater)
        setContentView(binding.root)
        storageBridge = AndroidStorageBridge(this)

        binding.toolbar.setNavigationOnClickListener { finish() }

        binding.btnSelectPhoto.setOnClickListener {
            pickPhotoLauncher.launch("image/*")
        }

        intent.data?.let { inspectUri(it) }
    }

    private fun inspectUri(uri: Uri) {
        lifecycleScope.launch {
            try {
                binding.btnSelectPhoto.isEnabled = false
                val tempFile = storageBridge.cacheUriToTempFile(uri, "inspect_")
                val fileName = storageBridge.getFileName(uri)

                val doc = metadataEngine.extractDocument(tempFile)

                // Thumbnail
                val thumb = storageBridge.createThumbnail(tempFile, 160)
                if (thumb != null) {
                    binding.ivThumbnail.setImageBitmap(thumb)
                }

                // Technical specs
                binding.tvFileName.text = fileName
                val sizeKb = doc.fileSizeBytes / 1024.0
                val sizeStr = if (sizeKb > 1024) "%.2f MB".format(sizeKb / 1024.0) else "%.1f KB".format(sizeKb)
                binding.tvTechnicalSpecs.text = "${doc.format} • ${doc.dimensions.first}x${doc.dimensions.second}px • $sizeStr"
                binding.tvSha256.text = "SHA-256: ${doc.sha256.take(24)}..."

                // Provenance Marker
                if (doc.marker != null) {
                    val dateStr = SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).format(doc.marker.processedAtUtc)
                    binding.tvMarkerDetails.text = "Status: PROCESSED & VERIFIED\n" +
                            "Profile: ${doc.marker.profile}\n" +
                            "Engine: v${doc.marker.engineVersion}\n" +
                            "Source Hash: ${doc.marker.sourceFingerprint.take(20)}...\n" +
                            "Processed: $dateStr UTC"
                } else {
                    binding.tvMarkerDetails.text = "Status: Not yet processed by PhotoForge (Original metadata state)"
                }

                // Camera & Optics
                val cam = doc.exif.camera
                val camLines = mutableListOf<String>()
                if (!cam.make.isNullOrBlank()) camLines.add("Make: ${cam.make}")
                if (!cam.model.isNullOrBlank()) camLines.add("Model: ${cam.model}")
                if (!cam.lensModel.isNullOrBlank()) camLines.add("Lens: ${cam.lensModel}")
                if (!cam.software.isNullOrBlank()) camLines.add("Software: ${cam.software}")
                binding.tvCameraDetails.text = if (camLines.isNotEmpty()) camLines.joinToString("\n") else "No camera equipment tags found."

                // Exposure
                val exp = doc.exif.exposure
                val expLines = mutableListOf<String>()
                if (doc.exif.dateTimeOriginal != null) {
                    val dt = SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).format(doc.exif.dateTimeOriginal)
                    expLines.add("Capture Time: $dt")
                }
                if (exp.iso != null) expLines.add("ISO: ${exp.iso}")
                if (exp.fNumber != null) expLines.add("Aperture: f/%.1f".format(exp.fNumber))
                if (exp.focalLengthMm != null) expLines.add("Focal Length: %.1fmm".format(exp.focalLengthMm))
                if (exp.exposureTimeSeconds != null) {
                    val expStr = if (exp.exposureTimeSeconds < 1.0 && exp.exposureTimeSeconds > 0) "1/%.0f".format(1.0 / exp.exposureTimeSeconds) else "%.1fs".format(exp.exposureTimeSeconds)
                    expLines.add("Shutter Speed: $expStr")
                }
                binding.tvExposureDetails.text = if (expLines.isNotEmpty()) expLines.joinToString("\n") else "No photographic exposure tags found."

                // GPS
                if (doc.gps != null) {
                    val altStr = if (doc.gps.altitudeMeters != null) " (Altitude: %.1fm)".format(doc.gps.altitudeMeters) else ""
                    binding.tvGpsDetails.text = "Latitude: %.6f\nLongitude: %.6f%s".format(doc.gps.latitude, doc.gps.longitude, altStr)
                } else {
                    binding.tvGpsDetails.text = "No GPS coordinates present in this image."
                }

                // IPTC
                val iptc = doc.iptc
                val iptcLines = mutableListOf<String>()
                if (iptc.keywords.isNotEmpty()) iptcLines.add("Keywords: ${iptc.keywords.joinToString(", ")}")
                if (!iptc.caption.isNullOrBlank()) iptcLines.add("Caption: ${iptc.caption}")
                if (!iptc.credit.isNullOrBlank()) iptcLines.add("Credit: ${iptc.credit}")
                if (!iptc.city.isNullOrBlank() || !iptc.country.isNullOrBlank()) iptcLines.add("Location: ${listOfNotNull(iptc.city, iptc.country).joinToString(", ")}")
                binding.tvIptcDetails.text = if (iptcLines.isNotEmpty()) iptcLines.joinToString("\n") else "No IPTC descriptive tags found."

                binding.layoutPhotoHeader.visibility = View.VISIBLE
                binding.layoutDetails.visibility = View.VISIBLE

                tempFile.delete()
            } catch (e: Exception) {
                Toast.makeText(this@InspectActivity, "Failed to inspect: ${e.message}", Toast.LENGTH_LONG).show()
            } finally {
                binding.btnSelectPhoto.isEnabled = true
            }
        }
    }
}
