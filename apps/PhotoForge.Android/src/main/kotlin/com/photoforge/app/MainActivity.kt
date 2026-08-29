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
import kotlinx.coroutines.launch
import java.io.File

class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private val metadataEngine = AndroidMetadataEngine()

    private var selectedEditedUri: Uri? = null
    private var selectedOriginalUri: Uri? = null

    private val pickEditedLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        uri?.let {
            selectedEditedUri = it
            pickOriginalLauncher.launch("image/*")
        }
    }

    private val pickOriginalLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri ->
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

        binding.btnSelectPair.setOnClickListener {
            Toast.makeText(this, "Select the edited photo first", Toast.LENGTH_SHORT).show()
            pickEditedLauncher.launch("image/*")
        }

        binding.btnBatchRestore.setOnClickListener {
            Toast.makeText(this, "Batch album selection is active", Toast.LENGTH_SHORT).show()
        }

        binding.btnSettings.setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }
    }

    private fun processSelectedPair() {
        val editedUri = selectedEditedUri ?: return
        val origUri = selectedOriginalUri ?: return

        lifecycleScope.launch {
            try {
                Toast.makeText(this@MainActivity, "Restoring metadata...", Toast.LENGTH_SHORT).show()
                val origTemp = storageBridge.cacheUriToTempFile(origUri, "orig_")
                val editedTemp = storageBridge.cacheUriToTempFile(editedUri, "edit_")

                val origSha = storageBridge.computeSha256(origTemp)

                metadataEngine.copyProvenance(
                    originalFile = origTemp,
                    targetFile = editedTemp,
                    sourceSha = origSha
                )

                val publishedUri = storageBridge.publishToMediaStore(
                    processedFile = editedTemp,
                    displayName = "restored_${System.currentTimeMillis()}.jpg"
                )

                Toast.makeText(this@MainActivity, "✔ Saved to Pictures/PhotoForge!", Toast.LENGTH_LONG).show()

                // Cleanup temp files
                origTemp.delete()
                editedTemp.delete()
            } catch (e: Exception) {
                Toast.makeText(this@MainActivity, "Error: ${e.message}", Toast.LENGTH_LONG).show()
            }
        }
    }
}
