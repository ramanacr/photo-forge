package com.photoforge.app

import android.graphics.Color
import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.photoforge.app.databinding.ActivityVerifyBinding
import com.photoforge.app.engine.AndroidImageEngine
import com.photoforge.app.engine.AndroidVerifierEngine
import com.photoforge.app.storage.AndroidStorageBridge
import kotlinx.coroutines.launch
import java.io.File

class VerifyActivity : AppCompatActivity() {

    private lateinit var binding: ActivityVerifyBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private val verifierEngine = AndroidVerifierEngine()
    private val imageEngine = AndroidImageEngine()

    private val pickPhotoLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let { verifyUri(it) }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityVerifyBinding.inflate(layoutInflater)
        setContentView(binding.root)
        storageBridge = AndroidStorageBridge(this)

        binding.toolbar.setNavigationOnClickListener { finish() }

        binding.btnSelectPhoto.setOnClickListener {
            pickPhotoLauncher.launch("image/*")
        }

        intent.data?.let { verifyUri(it) }
    }

    private fun verifyUri(uri: Uri) {
        lifecycleScope.launch {
            try {
                binding.btnSelectPhoto.isEnabled = false
                val tempFile = storageBridge.cacheUriToTempFile(uri, "verify_")
                val fileName = storageBridge.getFileName(uri)

                val format = imageEngine.sniffFormat(tempFile)
                val dims = imageEngine.inspectDimensions(tempFile)
                val sizeKb = tempFile.length() / 1024.0

                binding.tvFileName.text = fileName
                binding.tvFileSpecs.text = "$format • ${dims.first}x${dims.second}px • %.1f KB".format(sizeKb)

                val thumb = storageBridge.createThumbnail(tempFile, 140)
                if (thumb != null) {
                    binding.ivThumbnail.setImageBitmap(thumb)
                }

                val result = verifierEngine.verify(tempFile)

                // Big Badge
                if (result.isValid) {
                    binding.tvVerificationBadge.text = "✔ INTEGRITY & CONTINUITY PASSED"
                    binding.tvVerificationBadge.setTextColor(ContextCompat.getColor(this@VerifyActivity, R.color.accent_green))
                    binding.tvVerificationBadge.setBackgroundColor(Color.parseColor("#1B4332"))
                } else {
                    binding.tvVerificationBadge.text = "✘ VERIFICATION FAILED"
                    binding.tvVerificationBadge.setTextColor(ContextCompat.getColor(this@VerifyActivity, R.color.accent_red))
                    binding.tvVerificationBadge.setBackgroundColor(Color.parseColor("#4C1D1D"))
                }

                // Checklist
                binding.chkDecodable.text = if (result.canBeReopened) "✔ Image Stream Decodable (Bitmap Valid)" else "✘ Image Stream Corrupted"
                binding.chkDimensions.text = if (result.hasValidDimensions) "✔ Valid Pixel Dimensions (${dims.first}x${dims.second})" else "✘ Invalid Image Dimensions"
                binding.chkMetadata.text = if (result.hasRequiredMetadata) "✔ Photographic Metadata Present & Readable" else "⚠️ No Photographic Metadata Tags"
                binding.chkMarker.text = if (result.hasMigrationMarker) "✔ PhotoForge Provenance Marker (PF-MIG) Found" else "ℹ️ No PhotoForge Marker (Not yet processed)"

                // Verified attributes
                if (result.verifiedFields.isNotEmpty()) {
                    binding.tvVerifiedFields.text = result.verifiedFields.joinToString("\n• ", prefix = "• ")
                } else {
                    binding.tvVerifiedFields.text = "None"
                }

                // Errors
                if (result.errors.isNotEmpty()) {
                    binding.tvErrorsHeader.visibility = View.VISIBLE
                    binding.tvErrors.visibility = View.VISIBLE
                    binding.tvErrors.text = result.errors.joinToString("\n• ", prefix = "• ")
                } else {
                    binding.tvErrorsHeader.visibility = View.GONE
                    binding.tvErrors.visibility = View.GONE
                }

                binding.layoutPreview.visibility = View.VISIBLE
                binding.cardStatus.visibility = View.VISIBLE

                tempFile.delete()
            } catch (e: Exception) {
                Toast.makeText(this@VerifyActivity, "Verification failed: ${e.message}", Toast.LENGTH_LONG).show()
            } finally {
                binding.btnSelectPhoto.isEnabled = true
            }
        }
    }
}
