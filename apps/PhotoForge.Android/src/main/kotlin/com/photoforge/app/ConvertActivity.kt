package com.photoforge.app

import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.photoforge.app.databinding.ActivityConvertBinding
import com.photoforge.app.engine.AndroidImageEngine
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.model.ConversionQuality
import com.photoforge.app.model.GpsPrivacyPolicy
import com.photoforge.app.storage.AndroidStorageBridge
import com.photoforge.app.storage.PreferencesManager
import kotlinx.coroutines.launch
import java.io.File

class ConvertActivity : AppCompatActivity() {

    private lateinit var binding: ActivityConvertBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private lateinit var prefsManager: PreferencesManager
    private val imageEngine = AndroidImageEngine()
    private val metadataEngine = AndroidMetadataEngine()

    private var selectedUri: Uri? = null
    private var cachedInputFile: File? = null

    private val pickPhotoLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let { onPhotoSelected(it) }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityConvertBinding.inflate(layoutInflater)
        setContentView(binding.root)
        storageBridge = AndroidStorageBridge(this)
        prefsManager = PreferencesManager(this)

        binding.toolbar.setNavigationOnClickListener { finish() }

        binding.btnSelectPhoto.setOnClickListener {
            pickPhotoLauncher.launch("image/*")
        }

        binding.btnConvert.setOnClickListener {
            performConversion()
        }

        intent.data?.let { onPhotoSelected(it) }
    }

    private fun onPhotoSelected(uri: Uri) {
        selectedUri = uri
        lifecycleScope.launch {
            try {
                cachedInputFile?.delete()
                val tempFile = storageBridge.cacheUriToTempFile(uri, "conv_in_")
                cachedInputFile = tempFile

                val fileName = storageBridge.getFileName(uri)
                val format = imageEngine.sniffFormat(tempFile)
                val dims = imageEngine.inspectDimensions(tempFile)
                val sizeKb = tempFile.length() / 1024.0
                val sizeStr = if (sizeKb > 1024) "%.2f MB".format(sizeKb / 1024.0) else "%.1f KB".format(sizeKb)

                binding.tvInputName.text = fileName
                binding.tvInputSpecs.text = "Original: $format • ${dims.first}x${dims.second}px • $sizeStr"

                val thumb = storageBridge.createThumbnail(tempFile, 140)
                if (thumb != null) {
                    binding.ivThumbnail.setImageBitmap(thumb)
                }

                binding.layoutPreview.visibility = View.VISIBLE
                binding.cardResult.visibility = View.GONE
            } catch (e: Exception) {
                Toast.makeText(this@ConvertActivity, "Error loading image: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun performConversion() {
        val inputFile = cachedInputFile ?: run {
            Toast.makeText(this, "Please select a photo first", Toast.LENGTH_SHORT).show()
            return
        }

        val targetFormat = when {
            binding.rbWebp.isChecked -> "WEBP"
            binding.rbPng.isChecked -> "PNG"
            else -> "JPEG"
        }

        val quality = when {
            binding.rbLossless.isChecked -> ConversionQuality.LOSSLESS
            binding.rbVeryHigh.isChecked -> ConversionQuality.VERY_HIGH
            binding.rbBalanced.isChecked -> ConversionQuality.BALANCED
            binding.rbSmall.isChecked -> ConversionQuality.SMALL
            else -> ConversionQuality.HIGH
        }

        val ext = when (targetFormat) {
            "WEBP" -> ".webp"
            "PNG" -> ".png"
            else -> ".jpg"
        }

        val mimeType = when (targetFormat) {
            "WEBP" -> "image/webp"
            "PNG" -> "image/png"
            else -> "image/jpeg"
        }

        lifecycleScope.launch {
            try {
                binding.btnConvert.isEnabled = false
                binding.btnConvert.text = "Converting..."

                val outputFile = File.createTempFile("conv_out_", ext, cacheDir)

                val success = imageEngine.convertFormat(inputFile, outputFile, targetFormat, quality)
                if (!success) {
                    throw IllegalStateException("Failed to encode image to $targetFormat")
                }

                // Preserve metadata
                if (binding.swPreserveMeta.isChecked) {
                    val origSha = storageBridge.computeSha256(inputFile)
                    metadataEngine.copyProvenance(
                        originalFile = inputFile,
                        targetFile = outputFile,
                        sourceSha = origSha,
                        policy = prefsManager.gpsPrivacyPolicy,
                        profileName = "convert-$targetFormat".lowercase()
                    )
                }

                val origName = selectedUri?.let { storageBridge.getFileName(it).substringBeforeLast(".") } ?: "converted"
                val newName = "${origName}_converted$ext"

                val publishedUri = storageBridge.publishToMediaStore(outputFile, newName, mimeType)

                val inSizeKb = inputFile.length() / 1024.0
                val outSizeKb = outputFile.length() / 1024.0
                val delta = ((outSizeKb - inSizeKb) / inSizeKb) * 100.0
                val deltaStr = if (delta < 0) "%.1f%% smaller".format(-delta) else "%.1f%% larger".format(delta)

                binding.tvResultDetails.text = "Output: $newName\n" +
                        "Format: $targetFormat ($deltaStr)\n" +
                        "File Size: %.1f KB (was %.1f KB)\n".format(outSizeKb, inSizeKb) +
                        "Saved to: Pictures/PhotoForge"

                binding.cardResult.visibility = View.VISIBLE
                Toast.makeText(this@ConvertActivity, "✔ Converted & Saved to Gallery!", Toast.LENGTH_LONG).show()

                outputFile.delete()
            } catch (e: Exception) {
                Toast.makeText(this@ConvertActivity, "Conversion failed: ${e.message}", Toast.LENGTH_LONG).show()
            } finally {
                binding.btnConvert.isEnabled = true
                binding.btnConvert.text = "Convert & Save to Gallery"
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        cachedInputFile?.delete()
    }
}
