package com.photoforge.app.storage

import android.content.ContentValues
import android.content.Context
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileOutputStream
import java.io.InputStream
import java.security.MessageDigest

/**
 * Handles Android Scoped Storage, ContentResolver streaming, temp caching, and MediaStore publishing.
 */
class AndroidStorageBridge(private val context: Context) {

    /**
     * Streams a content:// URI into an app-private cache file for read-only inspection.
     */
    suspend fun cacheUriToTempFile(uri: Uri, prefix: String = "pf_input_"): File = withContext(Dispatchers.IO) {
        val tempFile = File.createTempFile(prefix, ".tmp", context.cacheDir)
        context.contentResolver.openInputStream(uri)?.use { input ->
            FileOutputStream(tempFile).use { output ->
                input.copyTo(output)
            }
        } ?: throw IllegalStateException("Unable to open stream for URI: $uri")
        tempFile
    }

    /**
     * Computes the SHA-256 fingerprint of a file to verify source immutability (INV-01).
     */
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

    /**
     * Publishes a processed photo back to the system MediaStore under Pictures/PhotoForge.
     */
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
}
