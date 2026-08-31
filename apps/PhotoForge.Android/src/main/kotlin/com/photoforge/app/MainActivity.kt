package com.photoforge.app

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.photoforge.app.databinding.ActivityMainBinding
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.storage.AndroidStorageBridge
import com.photoforge.app.storage.PreferencesManager
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private lateinit var prefsManager: PreferencesManager
    private val metadataEngine = AndroidMetadataEngine()

    private var selectedEditedUri: Uri? = null
    private var selectedOriginalUri: Uri? = null

    private val pickEditedLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let {
            selectedEditedUri = it
            Toast.makeText(this, "Edited selected. Now select matching original photo", Toast.LENGTH_SHORT).show()
            pickOriginalLauncher.launch("image/*")
        }
    }

    private val pickOriginalLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let {
            selectedOriginalUri = it
            processSelectedPair()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)
        storageBridge = AndroidStorageBridge(this)
        prefsManager = PreferencesManager(this)

        // Top Settings
        binding.btnSettings.setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }

        // Quick Restore
        binding.btnSelectPair.setOnClickListener {
            pickEditedLauncher.launch("image/*")
        }

        binding.btnDiffPreview.setOnClickListener {
            startActivity(Intent(this, DiffInspectorActivity::class.java))
        }

        // Batch Studio
        binding.btnOpenBatch.setOnClickListener {
            startActivity(Intent(this, BatchActivity::class.java))
        }
        binding.cardBatchRestore.setOnClickListener {
            startActivity(Intent(this, BatchActivity::class.java))
        }

        // Format Converter
        binding.btnOpenConvert.setOnClickListener {
            startActivity(Intent(this, ConvertActivity::class.java))
        }
        binding.cardConvert.setOnClickListener {
            startActivity(Intent(this, ConvertActivity::class.java))
        }

        // Match Review
        binding.btnOpenMatch.setOnClickListener {
            startActivity(Intent(this, MatchReviewActivity::class.java))
        }
        binding.cardMatchReview.setOnClickListener {
            startActivity(Intent(this, MatchReviewActivity::class.java))
        }

        // Inspect
        binding.btnOpenInspect.setOnClickListener {
            startActivity(Intent(this, InspectActivity::class.java))
        }
        binding.cardInspect.setOnClickListener {
            startActivity(Intent(this, InspectActivity::class.java))
        }

        // Verify
        binding.btnOpenVerify.setOnClickListener {
            startActivity(Intent(this, VerifyActivity::class.java))
        }
        binding.cardVerify.setOnClickListener {
            startActivity(Intent(this, VerifyActivity::class.java))
        }
    }

    private fun processSelectedPair() {
        val editedUri = selectedEditedUri ?: return
        val origUri = selectedOriginalUri ?: return

        lifecycleScope.launch {
            try {
                Toast.makeText(this@MainActivity, "Restoring metadata provenance...", Toast.LENGTH_SHORT).show()
                val origTemp = storageBridge.cacheUriToTempFile(origUri, "orig_")
                val editedTemp = storageBridge.cacheUriToTempFile(editedUri, "edit_")

                val origSha = storageBridge.computeSha256(origTemp)

                metadataEngine.copyProvenance(
                    originalFile = origTemp,
                    targetFile = editedTemp,
                    sourceSha = origSha,
                    policy = prefsManager.gpsPrivacyPolicy,
                    profileName = "quick-restore-v1"
                )

                val origName = storageBridge.getFileName(editedUri).substringBeforeLast(".")
                val publishedUri = storageBridge.publishToMediaStore(
                    processedFile = editedTemp,
                    displayName = "restored_${origName}.jpg"
                )

                Toast.makeText(this@MainActivity, "✔ Restored & Saved to Pictures/PhotoForge!", Toast.LENGTH_LONG).show()

                // Cleanup temp files
                origTemp.delete()
                editedTemp.delete()
                selectedEditedUri = null
                selectedOriginalUri = null
            } catch (e: Exception) {
                Toast.makeText(this@MainActivity, "Error: ${e.message}", Toast.LENGTH_LONG).show()
            }
        }
    }
}
