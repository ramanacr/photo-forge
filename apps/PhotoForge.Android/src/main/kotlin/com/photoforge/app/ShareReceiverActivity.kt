package com.photoforge.app

import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.storage.AndroidStorageBridge
import com.photoforge.app.storage.PreferencesManager
import kotlinx.coroutines.launch
import java.io.File

class ShareReceiverActivity : AppCompatActivity() {

    private lateinit var storageBridge: AndroidStorageBridge
    private lateinit var prefsManager: PreferencesManager
    private val metadataEngine = AndroidMetadataEngine()
    private val sharedUris = mutableListOf<Uri>()

    private val pickOriginalLauncher = registerForActivityResult(ActivityResultContracts.PickVisualMedia()) { origUri: Uri? ->
        if (origUri != null && sharedUris.isNotEmpty()) {
            processShareRestore(sharedUris.first(), origUri)
        } else {
            finish()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        storageBridge = AndroidStorageBridge(this)
        prefsManager = PreferencesManager(this)

        when (intent?.action) {
            Intent.ACTION_SEND -> {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    intent.getParcelableExtra(Intent.EXTRA_STREAM, Uri::class.java)?.let { uri ->
                        sharedUris.add(uri)
                    }
                } else {
                    @Suppress("DEPRECATION")
                    intent.getParcelableExtra<Uri>(Intent.EXTRA_STREAM)?.let { uri ->
                        sharedUris.add(uri)
                    }
                }
            }
            Intent.ACTION_SEND_MULTIPLE -> {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    intent.getParcelableArrayListExtra(Intent.EXTRA_STREAM, Uri::class.java)?.let { uris ->
                        sharedUris.addAll(uris)
                    }
                } else {
                    @Suppress("DEPRECATION")
                    intent.getParcelableArrayListExtra<Uri>(Intent.EXTRA_STREAM)?.let { uris ->
                        sharedUris.addAll(uris)
                    }
                }
            }
        }

        if (sharedUris.size > 1) {
            // Forward multiple photos to Batch Album Studio
            val batchIntent = Intent(this, BatchActivity::class.java).apply {
                putParcelableArrayListExtra(Intent.EXTRA_STREAM, ArrayList(sharedUris))
            }
            startActivity(batchIntent)
            finish()
        } else if (sharedUris.isNotEmpty()) {
            Toast.makeText(this, "PhotoForge: Select matching original camera photo", Toast.LENGTH_LONG).show()
            pickOriginalLauncher.launch(
                androidx.activity.result.PickVisualMediaRequest(
                    ActivityResultContracts.PickVisualMedia.ImageOnly
                )
            )
        } else {
            Toast.makeText(this, "No image shared with PhotoForge", Toast.LENGTH_SHORT).show()
            finish()
        }
    }

    private fun processShareRestore(sharedEditedUri: Uri, origUri: Uri) {
        lifecycleScope.launch {
            try {
                Toast.makeText(this@ShareReceiverActivity, "Restoring metadata from original...", Toast.LENGTH_SHORT).show()
                val origTemp = storageBridge.cacheUriToTempFile(origUri, "share_orig_")
                val editedTemp = storageBridge.cacheUriToTempFile(sharedEditedUri, "share_edit_")

                val origSha = storageBridge.computeSha256(origTemp)

                metadataEngine.copyProvenance(
                    originalFile = origTemp,
                    targetFile = editedTemp,
                    sourceSha = origSha,
                    policy = prefsManager.gpsPrivacyPolicy,
                    profileName = "share-restore-v1"
                )

                val name = storageBridge.getFileName(sharedEditedUri).substringBeforeLast(".")
                storageBridge.publishToMediaStore(
                    processedFile = editedTemp,
                    displayName = "restored_share_${name}.jpg"
                )

                Toast.makeText(this@ShareReceiverActivity, "✔ Restored and saved to Gallery!", Toast.LENGTH_LONG).show()

                origTemp.delete()
                editedTemp.delete()
                finish()
            } catch (e: Exception) {
                Toast.makeText(this@ShareReceiverActivity, "Restore failed: ${e.message}", Toast.LENGTH_LONG).show()
                finish()
            }
        }
    }
}
