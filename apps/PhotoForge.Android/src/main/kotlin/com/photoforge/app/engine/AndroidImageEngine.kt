package com.photoforge.app.engine

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Color
import android.os.Build
import com.photoforge.app.model.ConversionQuality
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream

class AndroidImageEngine {

    suspend fun sniffFormat(file: File): String = withContext(Dispatchers.IO) {
        if (!file.exists() || file.length() < 12) return@withContext "UNKNOWN"
        try {
            val header = ByteArray(12)
            FileInputStream(file).use { it.read(header) }

            // JPEG: FF D8 FF
            if (header[0] == 0xFF.toByte() && header[1] == 0xD8.toByte() && header[2] == 0xFF.toByte()) {
                return@withContext "JPEG"
            }
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89.toByte() && header[1] == 0x50.toByte() && header[2] == 0x4E.toByte() && header[3] == 0x47.toByte()) {
                return@withContext "PNG"
            }
            // WebP: RIFF .... WEBP
            if (header[0] == 0x52.toByte() && header[1] == 0x49.toByte() && header[2] == 0x46.toByte() && header[3] == 0x46.toByte() &&
                header[8] == 0x57.toByte() && header[9] == 0x45.toByte() && header[10] == 0x42.toByte() && header[11] == 0x50.toByte()) {
                return@withContext "WEBP"
            }
            // HEIC / HEIF: .... ftyp
            if (header[4] == 0x66.toByte() && header[5] == 0x74.toByte() && header[6] == 0x79.toByte() && header[7] == 0x70.toByte()) {
                return@withContext "HEIC"
            }
            // TIFF: II*. or MM.*
            if ((header[0] == 0x49.toByte() && header[1] == 0x49.toByte() && header[2] == 0x2A.toByte()) ||
                (header[0] == 0x4D.toByte() && header[1] == 0x4D.toByte() && header[3] == 0x2A.toByte())) {
                return@withContext "TIFF/RAW"
            }
        } catch (e: Exception) {
            // fallback
        }
        val ext = file.extension.uppercase()
        if (ext.isNotEmpty()) ext else "IMAGE"
    }

    suspend fun inspectDimensions(file: File): Pair<Int, Int> = withContext(Dispatchers.IO) {
        try {
            val options = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            BitmapFactory.decodeFile(file.absolutePath, options)
            Pair(options.outWidth, options.outHeight)
        } catch (e: Exception) {
            Pair(0, 0)
        }
    }

    suspend fun computePerceptualHash(file: File): ULong = withContext(Dispatchers.IO) {
        try {
            // Load and downsample to 9x8 grayscale
            val full = BitmapFactory.decodeFile(file.absolutePath) ?: return@withContext 0uL
            val scaled = Bitmap.createScaledBitmap(full, 9, 8, true)
            if (scaled != full) full.recycle()

            var hash = 0uL
            var bitIndex = 0

            for (y in 0 until 8) {
                for (x in 0 until 8) {
                    val pLeft = scaled.getPixel(x, y)
                    val pRight = scaled.getPixel(x + 1, y)

                    val grayLeft = (Color.red(pLeft) * 0.299 + Color.green(pLeft) * 0.587 + Color.blue(pLeft) * 0.114).toInt()
                    val grayRight = (Color.red(pRight) * 0.299 + Color.green(pRight) * 0.587 + Color.blue(pRight) * 0.114).toInt()

                    if (grayLeft > grayRight) {
                        hash = hash or (1uL shl bitIndex)
                    }
                    bitIndex++
                }
            }
            scaled.recycle()
            hash
        } catch (e: Exception) {
            0uL
        }
    }

    fun comparePerceptualHashes(hash1: ULong, hash2: ULong): Double {
        if (hash1 == 0uL && hash2 == 0uL) return 0.0
        val xor = hash1 xor hash2
        val diffBits = java.lang.Long.bitCount(xor.toLong())
        return Math.max(0.0, 1.0 - (diffBits.toDouble() / 64.0))
    }

    suspend fun convertFormat(
        inputFile: File,
        outputFile: File,
        targetFormat: String,
        quality: ConversionQuality
    ): Boolean = withContext(Dispatchers.IO) {
        try {
            val bitmap = BitmapFactory.decodeFile(inputFile.absolutePath) ?: return@withContext false
            val compressFormat: Bitmap.CompressFormat = when (targetFormat.uppercase()) {
                "WEBP" -> {
                    if (quality == ConversionQuality.LOSSLESS && Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                        Bitmap.CompressFormat.WEBP_LOSSLESS
                    } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                        Bitmap.CompressFormat.WEBP_LOSSY
                    } else {
                        @Suppress("DEPRECATION")
                        Bitmap.CompressFormat.WEBP
                    }
                }
                "PNG" -> Bitmap.CompressFormat.PNG
                else -> Bitmap.CompressFormat.JPEG
            }

            FileOutputStream(outputFile).use { out ->
                bitmap.compress(compressFormat, quality.qualityInt, out)
            }
            bitmap.recycle()
            true
        } catch (e: Exception) {
            false
        }
    }
}
