package com.photoforge.app.storage

import android.content.ContentValues
import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import android.provider.OpenableColumns
import androidx.documentfile.provider.DocumentFile
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileOutputStream
import java.security.MessageDigest

class AndroidStorageBridge(private val context: Context) {

    suspend fun cacheUriToTempFile(uri: Uri, prefix: String = "pf_input_"): File = withContext(Dispatchers.IO) {
        val extension = getExtensionFromUri(uri)
        val tempFile = File.createTempFile(prefix, extension, context.cacheDir)
        context.contentResolver.openInputStream(uri)?.use { input ->
            FileOutputStream(tempFile).use { output ->
                input.copyTo(output)
            }
        } ?: throw IllegalStateException("Unable to open stream for URI: $uri")
        tempFile
    }

    suspend fun computeSha256(file: File): String = withContext(Dispatchers.IO) {
        val digest = MessageDigest.getInstance("SHA-256")
        file.inputStream().use { input ->
            val buffer = ByteArray(8192)
            var read: Int
            while (input.read(buffer).also { read = it } > 0) {
                digest.update(buffer, 0, read)
            }
        }
        digest.digest().joinToString("") { "%02x".format(it) }
    }

    suspend fun publishToMediaStore(
        processedFile: File,
        displayName: String,
        mimeType: String = "image/jpeg"
    ): Uri = withContext(Dispatchers.IO) {
        val contentValues = ContentValues().apply {
            put(MediaStore.Images.Media.DISPLAY_NAME, displayName)
            put(MediaStore.Images.Media.MIME_TYPE, mimeType)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                put(MediaStore.Images.Media.RELATIVE_PATH, Environment.DIRECTORY_PICTURES + "/PhotoForge")
                put(MediaStore.Images.Media.IS_PENDING, 1)
            }
        }

        val collection = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            MediaStore.Images.Media.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY)
        } else {
            MediaStore.Images.Media.EXTERNAL_CONTENT_URI
        }

        val insertedUri = context.contentResolver.insert(collection, contentValues)
            ?: throw IllegalStateException("Failed to insert MediaStore record")

        context.contentResolver.openOutputStream(insertedUri)?.use { outStream ->
            processedFile.inputStream().use { inStream ->
                inStream.copyTo(outStream)
            }
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            contentValues.clear()
            contentValues.put(MediaStore.Images.Media.IS_PENDING, 0)
            context.contentResolver.update(insertedUri, contentValues, null, null)
        }

        insertedUri
    }

    fun getFileName(uri: Uri): String {
        var name = "photo_${System.currentTimeMillis()}"
        context.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) {
                val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                if (nameIndex != -1) {
                    name = cursor.getString(nameIndex) ?: name
                }
            }
        }
        return name
    }

    fun getFileSize(uri: Uri): Long {
        var size = 0L
        context.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) {
                val sizeIndex = cursor.getColumnIndex(OpenableColumns.SIZE)
                if (sizeIndex != -1) {
                    size = cursor.getLong(sizeIndex)
                }
            }
        }
        return size
    }

    fun listImagesFromTreeUri(treeUri: Uri): List<DocumentFile> {
        val root = DocumentFile.fromTreeUri(context, treeUri) ?: return emptyList()
        val results = mutableListOf<DocumentFile>()
        collectImagesRecursive(root, results)
        return results
    }

    private fun collectImagesRecursive(dir: DocumentFile, results: MutableList<DocumentFile>) {
        if (!dir.isDirectory) return
        for (file in dir.listFiles()) {
            if (file.isDirectory) {
                collectImagesRecursive(file, results)
            } else if (file.isFile) {
                val mime = file.type ?: ""
                val name = file.name?.lowercase() ?: ""
                if (mime.startsWith("image/") || name.endsWith(".jpg") || name.endsWith(".jpeg") ||
                    name.endsWith(".png") || name.endsWith(".webp") || name.endsWith(".heic") ||
                    name.endsWith(".heif") || name.endsWith(".dng") || name.endsWith(".raw")
                ) {
                    results.add(file)
                }
            }
        }
    }

    private fun getExtensionFromUri(uri: Uri): String {
        val mime = context.contentResolver.getType(uri)
        return when {
            mime?.contains("png", ignoreCase = true) == true -> ".png"
            mime?.contains("webp", ignoreCase = true) == true -> ".webp"
            mime?.contains("heic", ignoreCase = true) == true -> ".heic"
            mime?.contains("heif", ignoreCase = true) == true -> ".heif"
            else -> ".jpg"
        }
    }

    fun createThumbnail(file: File, maxDim: Int = 256): Bitmap? {
        return try {
            val options = BitmapFactory.Options().apply {
                inJustDecodeBounds = true
            }
            BitmapFactory.decodeFile(file.absolutePath, options)
            var inSampleSize = 1
            while (options.outWidth / (inSampleSize * 2) >= maxDim && options.outHeight / (inSampleSize * 2) >= maxDim) {
                inSampleSize *= 2
            }
            val decodeOptions = BitmapFactory.Options().apply {
                this.inSampleSize = inSampleSize
            }
            BitmapFactory.decodeFile(file.absolutePath, decodeOptions)
        } catch (e: Exception) {
            null
        }
    }
}
