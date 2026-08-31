package com.photoforge.app

import android.graphics.Color
import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.google.android.material.button.MaterialButton
import com.google.android.material.card.MaterialCardView
import com.photoforge.app.databinding.ActivityMatchReviewBinding
import com.photoforge.app.engine.AndroidImageEngine
import com.photoforge.app.engine.AndroidMatchingEngine
import com.photoforge.app.engine.AndroidMetadataEngine
import com.photoforge.app.model.ConfidenceBand
import com.photoforge.app.model.MatchingCandidate
import com.photoforge.app.storage.AndroidStorageBridge
import com.photoforge.app.storage.PreferencesManager
import kotlinx.coroutines.launch
import java.io.File

class MatchReviewActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMatchReviewBinding
    private lateinit var storageBridge: AndroidStorageBridge
    private lateinit var prefsManager: PreferencesManager
    private val matchingEngine = AndroidMatchingEngine()
    private val metadataEngine = AndroidMetadataEngine()
    private val imageEngine = AndroidImageEngine()

    private var targetFile: File? = null
    private val poolFiles = mutableListOf<File>()

    private val pickEditedLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let { onEditedSelected(it) }
    }

    private val pickPoolLauncher = registerForActivityResult(ActivityResultContracts.GetMultipleContents()) { uris: List<Uri> ->
        if (uris.isNotEmpty()) {
            onPoolSelected(uris)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMatchReviewBinding.inflate(layoutInflater)
        setContentView(binding.root)
        storageBridge = AndroidStorageBridge(this)
        prefsManager = PreferencesManager(this)

        binding.toolbar.setNavigationOnClickListener { finish() }

        binding.btnSelectEdited.setOnClickListener {
            pickEditedLauncher.launch("image/*")
        }

        binding.btnSelectPool.setOnClickListener {
            if (targetFile == null) {
                Toast.makeText(this, "Please select an edited photo first", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            Toast.makeText(this, "Select one or more original camera photos to evaluate", Toast.LENGTH_SHORT).show()
            pickPoolLauncher.launch("image/*")
        }
    }

    private fun onEditedSelected(uri: Uri) {
        lifecycleScope.launch {
            try {
                targetFile?.delete()
                val temp = storageBridge.cacheUriToTempFile(uri, "match_target_")
                targetFile = temp

                val fileName = storageBridge.getFileName(uri)
                val format = imageEngine.sniffFormat(temp)
                val dims = imageEngine.inspectDimensions(temp)

                binding.tvTargetName.text = fileName
                binding.tvTargetSpecs.text = "$format • ${dims.first}x${dims.second}px"

                val thumb = storageBridge.createThumbnail(temp, 140)
                if (thumb != null) {
                    binding.ivTargetThumbnail.setImageBitmap(thumb)
                }

                binding.layoutTargetPreview.visibility = View.VISIBLE
                binding.layoutCandidatesContainer.removeAllViews()
                binding.tvCandidatesTitle.visibility = View.GONE
            } catch (e: Exception) {
                Toast.makeText(this@MatchReviewActivity, "Error loading target photo: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun onPoolSelected(uris: List<Uri>) {
        val target = targetFile ?: return

        lifecycleScope.launch {
            try {
                binding.progressBar.visibility = View.VISIBLE
                binding.layoutCandidatesContainer.removeAllViews()
                binding.tvCandidatesTitle.visibility = View.GONE

                // Clean old pool
                poolFiles.forEach { it.delete() }
                poolFiles.clear()

                for (uri in uris) {
                    val file = storageBridge.cacheUriToTempFile(uri, "match_pool_")
                    poolFiles.add(file)
                }

                val candidates = matchingEngine.findCandidates(target, poolFiles)

                binding.progressBar.visibility = View.GONE

                if (candidates.isEmpty()) {
                    Toast.makeText(this@MatchReviewActivity, "No candidates found in selected pool", Toast.LENGTH_SHORT).show()
                    return@launch
                }

                binding.tvCandidatesTitle.visibility = View.VISIBLE
                renderCandidates(candidates)
            } catch (e: Exception) {
                binding.progressBar.visibility = View.GONE
                Toast.makeText(this@MatchReviewActivity, "Matching failed: ${e.message}", Toast.LENGTH_LONG).show()
            }
        }
    }

    private fun renderCandidates(candidates: List<MatchingCandidate>) {
        binding.layoutCandidatesContainer.removeAllViews()

        for (candidate in candidates) {
            val card = MaterialCardView(this).apply {
                radius = 16f
                strokeWidth = 2
                setCardBackgroundColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.card_dark))
                strokeColor = when (candidate.band) {
                    ConfidenceBand.AUTO_ACCEPT -> ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_green)
                    ConfidenceBand.SUGGESTED -> ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_cyan)
                    ConfidenceBand.USER_REVIEW_REQUIRED -> ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_yellow)
                    ConfidenceBand.NO_MATCH -> ContextCompat.getColor(this@MatchReviewActivity, R.color.border_dark)
                }
                layoutParams = LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT
                ).apply {
                    bottomMargin = 24
                }
            }

            val cardContent = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(24, 24, 24, 24)
            }

            // Top Header: Name & Score Badge
            val header = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
            }

            val nameView = TextView(this).apply {
                text = candidate.candidateName
                textSize = 14f
                setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.text_primary))
                setTypeface(null, android.graphics.Typeface.BOLD)
                layoutParams = LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f)
            }

            val scoreBadge = TextView(this).apply {
                val scorePercent = (candidate.score * 100).toInt()
                text = "$scorePercent% • ${candidate.band.label}"
                textSize = 11f
                setTypeface(null, android.graphics.Typeface.BOLD)
                setPadding(16, 8, 16, 8)

                when (candidate.band) {
                    ConfidenceBand.AUTO_ACCEPT -> {
                        setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_green))
                        setBackgroundColor(Color.parseColor("#1B4332"))
                    }
                    ConfidenceBand.SUGGESTED -> {
                        setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_cyan))
                        setBackgroundColor(Color.parseColor("#004B6E"))
                    }
                    ConfidenceBand.USER_REVIEW_REQUIRED -> {
                        setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_yellow))
                        setBackgroundColor(Color.parseColor("#583B06"))
                    }
                    ConfidenceBand.NO_MATCH -> {
                        setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.text_muted))
                        setBackgroundColor(Color.parseColor("#222222"))
                    }
                }
            }

            header.addView(nameView)
            header.addView(scoreBadge)
            cardContent.addView(header)

            // Reasons
            if (candidate.reasons.isNotEmpty()) {
                val reasonsView = TextView(this).apply {
                    text = candidate.reasons.joinToString("\n• ", prefix = "• ")
                    textSize = 12f
                    setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_cyan))
                    setPadding(0, 12, 0, 0)
                }
                cardContent.addView(reasonsView)
            }

            // Signal Breakdown
            val sig = candidate.signals
            val signalsText = "Signals: Name %.0f%% | Time %.0f%% | Dim %.0f%% | Visual %.0f%%".format(
                sig.filenameScore * 100,
                sig.timestampScore * 100,
                sig.dimensionsScore * 100,
                sig.perceptualSimilarityScore * 100
            )
            val signalsView = TextView(this).apply {
                text = signalsText
                textSize = 11f
                setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.text_muted))
                setPadding(0, 8, 0, 0)
            }
            cardContent.addView(signalsView)

            // Restore with this candidate button
            val restoreBtn = MaterialButton(this).apply {
                text = "⚡ Restore Using This Original"
                textSize = 12f
                cornerRadius = 16
                setBackgroundColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.accent_green))
                setTextColor(ContextCompat.getColor(this@MatchReviewActivity, R.color.bg_dark))
                layoutParams = LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT
                ).apply {
                    topMargin = 16
                }
                setOnClickListener {
                    applyCandidateRestore(candidate)
                }
            }
            cardContent.addView(restoreBtn)

            card.addView(cardContent)
            binding.layoutCandidatesContainer.addView(card)
        }
    }

    private fun applyCandidateRestore(candidate: MatchingCandidate) {
        val target = targetFile ?: return
        lifecycleScope.launch {
            try {
                Toast.makeText(this@MatchReviewActivity, "Restoring metadata from ${candidate.candidateName}...", Toast.LENGTH_SHORT).show()

                val workFile = File.createTempFile("restore_out_", ".jpg", cacheDir)
                target.copyTo(workFile, overwrite = true)

                val origSha = storageBridge.computeSha256(candidate.candidateFile)

                metadataEngine.copyProvenance(
                    originalFile = candidate.candidateFile,
                    targetFile = workFile,
                    sourceSha = origSha,
                    policy = prefsManager.gpsPrivacyPolicy,
                    profileName = "match-restore-v1"
                )

                val newName = "restored_${target.name.substringBeforeLast(".")}.jpg"
                storageBridge.publishToMediaStore(workFile, newName)
                workFile.delete()

                Toast.makeText(this@MatchReviewActivity, "✔ Successfully restored & saved to Pictures/PhotoForge!", Toast.LENGTH_LONG).show()
                finish()
            } catch (e: Exception) {
                Toast.makeText(this@MatchReviewActivity, "Restore failed: ${e.message}", Toast.LENGTH_LONG).show()
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        targetFile?.delete()
        poolFiles.forEach { it.delete() }
        poolFiles.clear()
    }
}
