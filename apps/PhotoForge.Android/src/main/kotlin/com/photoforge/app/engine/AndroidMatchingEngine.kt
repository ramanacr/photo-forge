package com.photoforge.app.engine

import com.photoforge.app.model.ConfidenceBand
import com.photoforge.app.model.MatchingCandidate
import com.photoforge.app.model.MetadataDocument
import com.photoforge.app.model.SignalScores
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.util.Locale
import java.util.regex.Pattern

class AndroidMatchingEngine(
    private val imageEngine: AndroidImageEngine = AndroidImageEngine(),
    private val metadataEngine: AndroidMetadataEngine = AndroidMetadataEngine()
) {

    private val suffixPattern = Pattern.compile(
        """[_\-\s]+(edited|edit|copy|final|export|modified|retouched|v\d+|\(\d+\)|\d{1,2})$""",
        Pattern.CASE_INSENSITIVE
    )

    private val relatedFolderNames = setOf(
        "originals", "original", "raw", "masters", "master", "source", "sources",
        "unedited", "camera", "dcim", "edited", "processed", "photoforge"
    )

    suspend fun findCandidates(
        targetFile: File,
        candidatePool: List<File>,
        targetMeta: MetadataDocument? = null,
        targetPhash: ULong? = null
    ): List<MatchingCandidate> = withContext(Dispatchers.IO) {
        val filteredPool = candidatePool.filter { it.absolutePath != targetFile.absolutePath }
        if (filteredPool.isEmpty()) return@withContext emptyList()

        val actualTargetMeta = targetMeta ?: metadataEngine.extractDocument(targetFile)
        val actualTargetPhash = targetPhash ?: imageEngine.computePerceptualHash(targetFile)

        val results = mutableListOf<MatchingCandidate>()

        for (candidate in filteredPool) {
            val candidateMeta = metadataEngine.extractDocument(candidate)
            val reasons = mutableListOf<String>()

            // 1. Filename Signal
            val (sFilename, rFilename) = evaluateFilename(targetFile.name, candidate.name)
            if (rFilename != null) reasons.add(rFilename)

            // 2. Timestamp Signal
            val (sTimestamp, rTimestamp) = evaluateTimestamp(actualTargetMeta, candidateMeta, targetFile, candidate)
            if (rTimestamp != null) reasons.add(rTimestamp)

            // 3. Dimensions Signal
            val (sDimensions, rDimensions) = evaluateDimensions(actualTargetMeta.dimensions, candidateMeta.dimensions)
            if (rDimensions != null) reasons.add(rDimensions)

            // 4. Metadata Remnants Signal
            val (sMetadata, rMetadata) = evaluateMetadataRemnants(actualTargetMeta, candidateMeta)
            if (rMetadata != null) reasons.add(rMetadata)

            // 5. Directory Signal
            val (sDirectory, rDirectory) = evaluateDirectory(targetFile, candidate)
            if (rDirectory != null) reasons.add(rDirectory)

            // 6. Perceptual Similarity Signal
            var sPerceptual = 0.0
            var candidatePhash: ULong? = null
            val preScore = (sFilename + sTimestamp + sDimensions + sMetadata + sDirectory) / 5.0

            if (preScore >= 0.30 || filteredPool.size <= 20) {
                candidatePhash = imageEngine.computePerceptualHash(candidate)
                if (actualTargetPhash != 0uL && candidatePhash != 0uL) {
                    sPerceptual = imageEngine.comparePerceptualHashes(actualTargetPhash, candidatePhash)
                    if (sPerceptual >= 0.85) {
                        reasons.add("High visual perceptual similarity (%.0f%%)".format(sPerceptual * 100))
                    } else if (sPerceptual >= 0.70) {
                        reasons.add("Moderate visual similarity (%.0f%%)".format(sPerceptual * 100))
                    }
                }
            }

            // Aggregate Score
            val aggregateScore = when {
                sPerceptual >= 0.70 && sFilename >= 0.80 -> {
                    0.85 + (0.10 * sPerceptual) + (0.05 * sDirectory)
                }
                sFilename >= 0.90 && sDirectory >= 0.90 -> {
                    0.85 + (0.10 * sDimensions) + (0.05 * sPerceptual)
                }
                actualTargetPhash != 0uL && candidatePhash != null && candidatePhash != 0uL -> {
                    (0.20 * sFilename) + (0.15 * sTimestamp) + (0.10 * sDimensions) +
                            (0.10 * sMetadata) + (0.35 * sPerceptual) + (0.10 * sDirectory)
                }
                else -> {
                    (0.30 * sFilename) + (0.25 * sTimestamp) + (0.15 * sDimensions) +
                            (0.15 * sMetadata) + (0.15 * sDirectory)
                }
            }.coerceIn(0.0, 1.0)

            val signals = SignalScores(
                filenameScore = sFilename,
                timestampScore = sTimestamp,
                dimensionsScore = sDimensions,
                metadataRemnantsScore = sMetadata,
                perceptualSimilarityScore = sPerceptual,
                directoryRelationScore = sDirectory,
                aggregateScore = aggregateScore
            )

            val band = ConfidenceBand.fromScore(aggregateScore)

            results.add(
                MatchingCandidate(
                    candidateFile = candidate,
                    candidateName = candidate.name,
                    score = aggregateScore,
                    band = band,
                    signals = signals,
                    reasons = reasons,
                    perceptualHash = candidatePhash
                )
            )
        }

        results.sortedByDescending { it.score }
    }

    private fun evaluateFilename(targetName: String, candidateName: String): Pair<Double, String?> {
        val tBase = targetName.substringBeforeLast(".").lowercase(Locale.US)
        val cBase = candidateName.substringBeforeLast(".").lowercase(Locale.US)

        if (tBase == cBase) {
            return Pair(1.0, "Exact matching base filename")
        }

        var tClean = tBase
        var prev: String
        do {
            prev = tClean
            tClean = suffixPattern.matcher(tClean).replaceAll("").trim()
        } while (tClean != prev)

        var cClean = cBase
        do {
            prev = cClean
            cClean = suffixPattern.matcher(cClean).replaceAll("").trim()
        } while (cClean != prev)

        if (tClean == cClean && tClean.isNotEmpty()) {
            return Pair(0.95, "Matching base filename with common edit suffix stripped")
        }

        if (tBase.contains(cBase) || cBase.contains(tBase) || (tClean.isNotEmpty() && (tClean.contains(cClean) || cClean.contains(tClean)))) {
            return Pair(0.85, "Filename containment relationship")
        }

        val maxLen = maxOf(tClean.length, cClean.length)
        if (maxLen == 0) return Pair(0.0, null)

        val dist = levenshteinDistance(tClean, cClean)
        val sim = (1.0 - (dist.toDouble() / maxLen)).coerceAtLeast(0.0)

        return if (sim > 0.75) {
            Pair(sim, "High filename similarity (%.0f%%)".format(sim * 100))
        } else {
            Pair(sim, null)
        }
    }

    private fun evaluateTimestamp(
        target: MetadataDocument,
        candidate: MetadataDocument,
        targetFile: File,
        candidateFile: File
    ): Pair<Double, String?> {
        val tCap = target.bestCaptureDate
        val cCap = candidate.bestCaptureDate

        if (tCap != null && cCap != null) {
            val diffMs = Math.abs(tCap.time - cCap.time)
            val diffSec = diffMs / 1000.0
            val diffMin = diffSec / 60.0

            return when {
                diffSec < 2 -> Pair(1.0, "Exact same capture timestamp")
                diffMin < 1 -> Pair(0.95, "Capture timestamps within %.0fs".format(diffSec))
                diffMin < 10 -> Pair(0.85, "Capture timestamps within %.0f min".format(diffMin))
                diffMin < 60 -> Pair(0.60, null)
                else -> Pair(0.0, null)
            }
        }

        if (cCap != null) {
            val tMod = targetFile.lastModified()
            val diffDays = (tMod - cCap.time) / (1000.0 * 60 * 60 * 24)
            if (diffDays in -1.0..30.0) {
                return Pair(0.65, "Target modification date is shortly after capture")
            }
        }

        return Pair(0.40, null)
    }

    private fun evaluateDimensions(targetDim: Pair<Int, Int>, candidateDim: Pair<Int, Int>): Pair<Double, String?> {
        if (targetDim.first <= 0 || targetDim.second <= 0 || candidateDim.first <= 0 || candidateDim.second <= 0) {
            return Pair(0.5, null)
        }

        if (targetDim.first == candidateDim.first && targetDim.second == candidateDim.second) {
            return Pair(1.0, "Exact identical pixel dimensions")
        }

        val arTarget = targetDim.first.toDouble() / targetDim.second
        val arCandidate = candidateDim.first.toDouble() / candidateDim.second
        val arRotated = candidateDim.second.toDouble() / candidateDim.first

        if (Math.abs(arTarget - arCandidate) < 0.02 || Math.abs(arTarget - arRotated) < 0.02) {
            return Pair(0.90, "Matching aspect ratio")
        }

        return Pair(0.40, null)
    }

    private fun evaluateMetadataRemnants(target: MetadataDocument, candidate: MetadataDocument): Pair<Double, String?> {
        val tCam = target.exif.camera
        val cCam = candidate.exif.camera

        val hasTargetMake = !tCam.make.isNullOrBlank()
        val hasTargetModel = !tCam.model.isNullOrBlank()

        if (hasTargetModel && !cCam.model.isNullOrBlank()) {
            return if (tCam.model.equals(cCam.model, ignoreCase = true)) {
                Pair(1.0, "Same camera model (${cCam.model})")
            } else {
                Pair(0.0, null)
            }
        }

        if (hasTargetMake && !cCam.make.isNullOrBlank()) {
            return if (tCam.make.equals(cCam.make, ignoreCase = true)) {
                Pair(0.85, "Same camera make (${cCam.make})")
            } else {
                Pair(0.1, null)
            }
        }

        return Pair(0.5, null)
    }

    private fun evaluateDirectory(targetFile: File, candidateFile: File): Pair<Double, String?> {
        val tDir = targetFile.parentFile
        val cDir = candidateFile.parentFile

        if (tDir?.absolutePath == cDir?.absolutePath) {
            return Pair(1.0, "Same directory")
        }

        val tFolder = tDir?.name?.lowercase(Locale.US) ?: ""
        val cFolder = cDir?.name?.lowercase(Locale.US) ?: ""

        if (relatedFolderNames.contains(tFolder) && relatedFolderNames.contains(cFolder)) {
            return Pair(0.90, "Paired workflow directories ($cFolder -> $tFolder)")
        }

        if (tDir?.parentFile?.absolutePath == cDir?.parentFile?.absolutePath) {
            return Pair(0.75, "Sibling directories under same parent")
        }

        return Pair(0.20, null)
    }

    private fun levenshteinDistance(s: String, t: String): Int {
        val n = s.length
        val m = t.length
        val d = Array(n + 1) { IntArray(m + 1) }

        for (i in 0..n) d[i][0] = i
        for (j in 0..m) d[0][j] = j

        for (i in 1..n) {
            for (j in 1..m) {
                val cost = if (s[i - 1] == t[j - 1]) 0 else 1
                d[i][j] = minOf(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + cost)
            }
        }
        return d[n][m]
    }
}
