package com.photoforge.app.engine

import com.photoforge.app.model.*
import com.photoforge.app.storage.AndroidStorageBridge
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File

class AndroidBatchEngine(
    private val matchingEngine: AndroidMatchingEngine = AndroidMatchingEngine(),
    private val metadataEngine: AndroidMetadataEngine = AndroidMetadataEngine(),
    private val storageBridge: AndroidStorageBridge
) {

    suspend fun processBatch(
        editedFiles: List<File>,
        originalPool: List<File>,
        policy: GpsPrivacyPolicy = GpsPrivacyPolicy.KEEP_EXACT,
        autoAcceptThreshold: Double = 0.75,
        onProgress: (Int, Int, String) -> Unit
    ): BatchSummary = withContext(Dispatchers.IO) {
        val startTime = System.currentTimeMillis()
        val itemResults = mutableListOf<BatchItemResult>()

        var succeeded = 0
        var warnings = 0
        var skipped = 0
        var reviewRequired = 0
        var failed = 0

        val total = editedFiles.size

        for ((index, editedFile) in editedFiles.withIndex()) {
            onProgress(index + 1, total, editedFile.name)

            try {
                // Find candidates for this edited file
                val candidates = matchingEngine.findCandidates(editedFile, originalPool)
                val bestCandidate = candidates.firstOrNull()

                if (bestCandidate != null && bestCandidate.score >= autoAcceptThreshold) {
                    val origFile = bestCandidate.candidateFile
                    val origSha = storageBridge.computeSha256(origFile)

                    // Make a temp working copy of the edited file to avoid mutating in place
                    val workCopy = File.createTempFile("batch_work_", ".${editedFile.extension}", editedFile.parentFile)
                    editedFile.copyTo(workCopy, overwrite = true)

                    val diff = metadataEngine.copyProvenance(
                        originalFile = origFile,
                        targetFile = workCopy,
                        sourceSha = origSha,
                        policy = policy
                    )

                    val publishedUri = storageBridge.publishToMediaStore(
                        processedFile = workCopy,
                        displayName = "restored_${editedFile.name}"
                    )
                    workCopy.delete()

                    if (diff.hasWarnings) {
                        warnings++
                        itemResults.add(
                            BatchItemResult(
                                targetName = editedFile.name,
                                originalName = origFile.name,
                                status = BatchItemStatus.WARNING,
                                outputPath = publishedUri.toString(),
                                message = "Restored with ${diff.warnings.size} warning(s) (Match score: %.0f%%)".format(bestCandidate.score * 100)
                            )
                        )
                    } else {
                        succeeded++
                        itemResults.add(
                            BatchItemResult(
                                targetName = editedFile.name,
                                originalName = origFile.name,
                                status = BatchItemStatus.SUCCESS,
                                outputPath = publishedUri.toString(),
                                message = "Restored successfully (Match score: %.0f%%)".format(bestCandidate.score * 100)
                            )
                        )
                    }
                } else if (bestCandidate != null && bestCandidate.score >= 0.60) {
                    reviewRequired++
                    skipped++
                    itemResults.add(
                        BatchItemResult(
                            targetName = editedFile.name,
                            originalName = bestCandidate.candidateName,
                            status = BatchItemStatus.SKIPPED,
                            outputPath = null,
                            message = "Review Required: Match score %.0f%% below auto-accept threshold".format(bestCandidate.score * 100)
                        )
                    )
                } else {
                    skipped++
                    itemResults.add(
                        BatchItemResult(
                            targetName = editedFile.name,
                            originalName = null,
                            status = BatchItemStatus.SKIPPED,
                            outputPath = null,
                            message = "No matching original found in gallery pool"
                        )
                    )
                }
            } catch (e: Exception) {
                failed++
                itemResults.add(
                    BatchItemResult(
                        targetName = editedFile.name,
                        originalName = null,
                        status = BatchItemStatus.FAILED,
                        outputPath = null,
                        message = "Processing error: ${e.message}"
                    )
                )
            }
        }

        val duration = System.currentTimeMillis() - startTime

        BatchSummary(
            totalItems = total,
            succeededCount = succeeded,
            warningsCount = warnings,
            skippedCount = skipped,
            reviewRequiredCount = reviewRequired,
            failedCount = failed,
            durationMs = duration,
            itemResults = itemResults
        )
    }
}
