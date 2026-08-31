package com.photoforge.app

import android.graphics.Color
import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.google.android.material.card.MaterialCardView
import com.photoforge.app.databinding.ActivityBatchBinding
import com.photoforge.app.engine.AndroidBatchEngine
import com.photoforge.app.engine.AndroidMatchingEngine
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.model.BatchItemStatus
import com.photoforge.app.model.BatchSummary
import com.photoforge.app.storage.AndroidStorageBridge
import com.photoforge.app.storage.PreferencesManager
import kotlinx.coroutines.launch
import java.io.File

class BatchActivity : AppCompatActivity() {

    private lateinit var binding: ActivityBatchBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private lateinit var prefsManager: PreferencesManager
    private lateinit var batchEngine: AndroidBatchEngine

    private val editedUris = mutableListOf<Uri>()
    private val originalUris = mutableListOf<Uri>()

    private val pickEditedLauncher = registerForActivityResult(ActivityResultContracts.GetMultipleContents()) { uris: List<Uri> ->
        if (uris.isNotEmpty()) {
            editedUris.clear()
            editedUris.addAll(uris)
            binding.tvEditedCount.text = "${editedUris.size} edited photo(s) selected"
        }
    }

    private val pickOriginalsLauncher = registerForActivityResult(ActivityResultContracts.GetMultipleContents()) { uris: List<Uri> ->
        if (uris.isNotEmpty()) {
            originalUris.clear()
            originalUris.addAll(uris)
            binding.tvOriginalsCount.text = "${originalUris.size} original camera photo(s) selected"
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityBatchBinding.inflate(layoutInflater)
        setContentView(binding.root)
        storageBridge = AndroidStorageBridge(this)
        prefsManager = PreferencesManager(this)
        batchEngine = AndroidBatchEngine(
            matchingEngine = AndroidMatchingEngine(),
            metadataEngine = AndroidMetadataEngine(),
            storageBridge = storageBridge
        )

        binding.toolbar.setNavigationOnClickListener { finish() }

        binding.btnSelectEdited.setOnClickListener {
            pickEditedLauncher.launch("image/*")
        }

        binding.btnSelectOriginals.setOnClickListener {
            pickOriginalsLauncher.launch("image/*")
        }

        binding.btnStartBatch.setOnClickListener {
            startBatchExecution()
        }
    }

    private fun startBatchExecution() {
        if (editedUris.isEmpty()) {
            Toast.makeText(this, "Please select edited photos first", Toast.LENGTH_SHORT).show()
            return
        }
        if (originalUris.isEmpty()) {
            Toast.makeText(this, "Please select originals pool photos", Toast.LENGTH_SHORT).show()
            return
        }

        lifecycleScope.launch {
            try {
                binding.btnStartBatch.isEnabled = false
                binding.layoutProgress.visibility = View.VISIBLE
                binding.cardSummary.visibility = View.GONE
                binding.layoutItemsContainer.removeAllViews()

                binding.tvProgressStatus.text = "Caching selected photos..."
                binding.progressBar.progress = 0

                val cachedEditedFiles = mutableListOf<File>()
                for (uri in editedUris) {
                    cachedEditedFiles.add(storageBridge.cacheUriToTempFile(uri, "batch_edit_"))
                }

                val cachedOriginalFiles = mutableListOf<File>()
                for (uri in originalUris) {
                    cachedOriginalFiles.add(storageBridge.cacheUriToTempFile(uri, "batch_orig_"))
                }

                val threshold = if (binding.swAutoAccept.isChecked) 0.75 else 0.90

                val summary = batchEngine.processBatch(
                    editedFiles = cachedEditedFiles,
                    originalPool = cachedOriginalFiles,
                    policy = prefsManager.gpsPrivacyPolicy,
                    autoAcceptThreshold = threshold
                ) { current, total, name ->
                    val percent = ((current.toDouble() / total) * 100).toInt()
                    binding.progressBar.progress = percent
                    binding.tvProgressStatus.text = "Processing $current of $total: $name ($percent%)"
                }

                binding.layoutProgress.visibility = View.GONE
                displaySummary(summary)

                // Cleanup
                cachedEditedFiles.forEach { it.delete() }
                cachedOriginalFiles.forEach { it.delete() }

                Toast.makeText(this@BatchActivity, "✔ Batch processing complete!", Toast.LENGTH_LONG).show()
            } catch (e: Exception) {
                binding.layoutProgress.visibility = View.GONE
                Toast.makeText(this@BatchActivity, "Batch failed: ${e.message}", Toast.LENGTH_LONG).show()
            } finally {
                binding.btnStartBatch.isEnabled = true
            }
        }
    }

    private fun displaySummary(summary: BatchSummary) {
        val durationSec = summary.durationMs / 1000.0
        binding.tvSummaryDetails.text = "Total Processed: ${summary.totalItems} images in %.1fs\n".format(durationSec) +
                "✔ Succeeded: ${summary.succeededCount}  |  ⚠️ Warnings: ${summary.warningsCount}\n" +
                "⏸ Skipped / Review Needed: ${summary.skippedCount}  |  ✘ Failed: ${summary.failedCount}"

        binding.cardSummary.visibility = View.VISIBLE

        binding.layoutItemsContainer.removeAllViews()

        for (item in summary.itemResults) {
            val card = MaterialCardView(this).apply {
                radius = 12f
                setCardBackgroundColor(ContextCompat.getColor(this@BatchActivity, R.color.card_dark))
                layoutParams = LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT
                ).apply {
                    bottomMargin = 16
                }
            }

            val cardContent = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(20, 16, 20, 16)
            }

            val header = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
            }

            val nameView = TextView(this).apply {
                text = item.targetName
                textSize = 13f
                setTextColor(ContextCompat.getColor(this@BatchActivity, R.color.text_primary))
                setTypeface(null, android.graphics.Typeface.BOLD)
                layoutParams = LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f)
            }

            val statusBadge = TextView(this).apply {
                text = item.status.name
                textSize = 11f
                setTypeface(null, android.graphics.Typeface.BOLD)
                setPadding(14, 6, 14, 6)

                when (item.status) {
                    BatchItemStatus.SUCCESS -> {
                        setTextColor(ContextCompat.getColor(this@BatchActivity, R.color.accent_green))
                        setBackgroundColor(Color.parseColor("#1B4332"))
                    }
                    BatchItemStatus.WARNING -> {
                        setTextColor(ContextCompat.getColor(this@BatchActivity, R.color.accent_yellow))
                        setBackgroundColor(Color.parseColor("#583B06"))
                    }
                    BatchItemStatus.SKIPPED -> {
                        setTextColor(ContextCompat.getColor(this@BatchActivity, R.color.text_muted))
                        setBackgroundColor(Color.parseColor("#222222"))
                    }
                    BatchItemStatus.FAILED -> {
                        setTextColor(ContextCompat.getColor(this@BatchActivity, R.color.accent_red))
                        setBackgroundColor(Color.parseColor("#4C1D1D"))
                    }
                }
            }

            header.addView(nameView)
            header.addView(statusBadge)
            cardContent.addView(header)

            val msgView = TextView(this).apply {
                text = item.message
                textSize = 11f
                setTextColor(ContextCompat.getColor(this@BatchActivity, R.color.text_secondary))
                setPadding(0, 6, 0, 0)
            }
            cardContent.addView(msgView)

            card.addView(cardContent)
            binding.layoutItemsContainer.addView(card)
        }
    }
}
