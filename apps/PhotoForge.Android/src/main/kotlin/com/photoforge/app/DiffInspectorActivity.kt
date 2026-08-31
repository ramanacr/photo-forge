package com.photoforge.app

import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.photoforge.app.databinding.ActivityDiffInspectorBinding
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.model.MetadataDiff
import com.photoforge.app.storage.AndroidStorageBridge
import com.photoforge.app.storage.PreferencesManager
import kotlinx.coroutines.launch
import java.io.File

class DiffInspectorActivity : AppCompatActivity() {

    private lateinit var binding: ActivityDiffInspectorBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private lateinit var prefsManager: PreferencesManager
    private val metadataEngine = AndroidMetadataEngine()

    private var origFile: File? = null
    private var editedFile: File? = null
    private var currentDiff: MetadataDiff? = null

    private val pickOriginalLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let { onOriginalSelected(it) }
    }

    private val pickEditedLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let { onEditedSelected(it) }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityDiffInspectorBinding.inflate(layoutInflater)
        setContentView(binding.root)
        storageBridge = AndroidStorageBridge(this)
        prefsManager = PreferencesManager(this)

        binding.toolbar.setNavigationOnClickListener { finish() }

        binding.btnSelectOriginal.setOnClickListener {
            pickOriginalLauncher.launch("image/*")
        }

        binding.btnSelectEdited.setOnClickListener {
            pickEditedLauncher.launch("image/*")
        }

        binding.btnApplyDiff.setOnClickListener {
            applyProvenanceAndSave()
        }
    }

    private fun onOriginalSelected(uri: Uri) {
        lifecycleScope.launch {
            try {
                origFile?.delete()
                origFile = storageBridge.cacheUriToTempFile(uri, "diff_orig_")
                binding.btnSelectOriginal.text = "✔ Orig: ${storageBridge.getFileName(uri)}"
                tryComputeDiff()
            } catch (e: Exception) {
                Toast.makeText(this@DiffInspectorActivity, "Failed to load original: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun onEditedSelected(uri: Uri) {
        lifecycleScope.launch {
            try {
                editedFile?.delete()
                editedFile = storageBridge.cacheUriToTempFile(uri, "diff_edit_")
                binding.btnSelectEdited.text = "✔ Edit: ${storageBridge.getFileName(uri)}"
                tryComputeDiff()
            } catch (e: Exception) {
                Toast.makeText(this@DiffInspectorActivity, "Failed to load edited: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun tryComputeDiff() {
        val oFile = origFile ?: return
        val eFile = editedFile ?: return

        lifecycleScope.launch {
            try {
                val origDoc = metadataEngine.extractDocument(oFile)
                val editedDoc = metadataEngine.extractDocument(eFile)

                val diff = metadataEngine.computeDiff(origDoc, editedDoc, prefsManager.gpsPrivacyPolicy)
                currentDiff = diff

                // Copied
                binding.tvCopiedHeader.text = "📥 Copied from Original (${diff.copiedFromOriginal.size} provenance tags)"
                binding.tvCopiedDetails.text = if (diff.copiedFromOriginal.isNotEmpty()) {
                    diff.copiedFromOriginal.joinToString("\n• ", prefix = "• ")
                } else {
                    "No tags to copy"
                }

                // Preserved
                binding.tvPreservedHeader.text = "🛡️ Preserved from Edited Target (${diff.preservedFromTarget.size} tags)"
                binding.tvPreservedDetails.text = if (diff.preservedFromTarget.isNotEmpty()) {
                    diff.preservedFromTarget.joinToString("\n• ", prefix = "• ")
                } else {
                    "No custom tags to preserve (standard image)"
                }

                // Warnings
                if (diff.warnings.isNotEmpty()) {
                    binding.cardWarnings.visibility = View.VISIBLE
                    binding.tvWarningsDetails.text = diff.warnings.joinToString("\n• ", prefix = "• ")
                } else {
                    binding.cardWarnings.visibility = View.GONE
                }

                binding.layoutDiffContent.visibility = View.VISIBLE
            } catch (e: Exception) {
                Toast.makeText(this@DiffInspectorActivity, "Diff computation error: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun applyProvenanceAndSave() {
        val oFile = origFile ?: return
        val eFile = editedFile ?: return

        lifecycleScope.launch {
            try {
                binding.btnApplyDiff.isEnabled = false
                binding.btnApplyDiff.text = "Applying..."

                val workFile = File.createTempFile("diff_save_", ".jpg", cacheDir)
                eFile.copyTo(workFile, overwrite = true)

                val origSha = storageBridge.computeSha256(oFile)

                metadataEngine.copyProvenance(
                    originalFile = oFile,
                    targetFile = workFile,
                    sourceSha = origSha,
                    policy = prefsManager.gpsPrivacyPolicy,
                    profileName = "diff-inspect-v1"
                )

                val publishedUri = storageBridge.publishToMediaStore(
                    processedFile = workFile,
                    displayName = "restored_diff_${System.currentTimeMillis()}.jpg"
                )
                workFile.delete()

                Toast.makeText(this@DiffInspectorActivity, "✔ Saved to Pictures/PhotoForge!", Toast.LENGTH_LONG).show()
                finish()
            } catch (e: Exception) {
                Toast.makeText(this@DiffInspectorActivity, "Save failed: ${e.message}", Toast.LENGTH_LONG).show()
            } finally {
                binding.btnApplyDiff.isEnabled = true
                binding.btnApplyDiff.text = "Apply Provenance & Save"
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        origFile?.delete()
        editedFile?.delete()
    }
}
