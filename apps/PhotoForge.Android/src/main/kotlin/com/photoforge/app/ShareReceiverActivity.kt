package com.photoforge.app

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.storage.AndroidStorageBridge
import kotlinx.coroutines.launch
import java.io.File

/**
 * Handles Android system Share Sheet (SEND and SEND_MULTIPLE) intents from Gallery / Google Photos.
 */
class ShareReceiverActivity : AppCompatActivity() {

    private lateinit var storageBridge: AndroidStorageBridge
    private val metadataEngine = AndroidMetadataEngine()
    private var sharedUris = mutableListOf<Uri>()

    private val pickOriginalLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { origUri ->
        if (origUri != null && sharedUris.isNotEmpty()) {
            processShareRestore(sharedUris.first(), origUri)
        } else {
            finish()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        storageBridge = AndroidStorageBridge(this)

        when (intent?.action) {
            Intent.ACTION_SEND -> {
                (intent.getParcelableExtra<Uri>(Intent.EXTRA_STREAM))?.let { uri ->
                    sharedUris.add(uri)
                }
            }
            Intent.ACTION_SEND_MULTIPLE -> {
                intent.getParcelableArrayListExtra<Uri>(Intent.EXTRA_STREAM)?.let { uris ->
                    sharedUris.addAll(uris)
                }
            }
        }

        if (sharedUris.isNotEmpty()) {
            Toast.makeText(this, "PhotoForge: Select matching original camera photo", Toast.LENGTH_LONG).show()
            pickOriginalLauncher.launch("image/*")
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
                    sourceSha = origSha
                )

                storageBridge.publishToMediaStore(
                    processedFile = editedTemp,
                    displayName = "restored_share_${System.currentTimeMillis()}.jpg"
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
