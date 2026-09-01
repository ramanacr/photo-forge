package com.photoforge.app.engine

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.io.File
import java.io.FileOutputStream
import java.net.HttpURLConnection
import java.net.URL

data class AndroidUpdateInfo(
    val currentVersion: String,
    val latestVersion: String,
    val isUpdateAvailable: Boolean,
    val releaseTitle: String,
    val releaseNotes: String,
    val downloadUrl: String,
    val fileName: String,
    val releaseHtmlUrl: String
)

class AndroidUpdateEngine {

    companion object {
        private const val GITHUB_REPO_OWNER = "ramanacr"
        private const val GITHUB_REPO_NAME = "photo-forge"
        private const val GITHUB_API_URL = "https://api.github.com/repos/$GITHUB_REPO_OWNER/$GITHUB_REPO_NAME/releases/latest"
    }

    suspend fun checkForUpdates(currentVersionName: String): AndroidUpdateInfo? = withContext(Dispatchers.IO) {
        try {
            val url = URL(GITHUB_API_URL)
            val connection = (url.openConnection() as HttpURLConnection).apply {
                requestMethod = "GET"
                setRequestProperty("Accept", "application/vnd.github.v3+json")
                setRequestProperty("User-Agent", "PhotoForge-Android/$currentVersionName")
                connectTimeout = 10000
                readTimeout = 10000
            }

            if (connection.responseCode != HttpURLConnection.HTTP_OK) {
                return@withContext null
            }

            val jsonText = connection.inputStream.bufferedReader().use { it.readText() }
            val root = JSONObject(jsonText)

            val tagName = root.optString("tag_name", "v1.0.0")
            val releaseTitle = root.optString("name", tagName)
            val releaseNotes = root.optString("body", "")
            val htmlUrl = root.optString("html_url", "https://github.com/$GITHUB_REPO_OWNER/$GITHUB_REPO_NAME/releases")

            val cleanLatest = tagName.trimStart('v', 'V').split("-")[0]
            val cleanCurrent = currentVersionName.trimStart('v', 'V').split("-")[0]

            val isUpdateAvailable = isNewerVersion(cleanLatest, cleanCurrent)

            var apkUrl = ""
            var apkName = ""

            val assets = root.optJSONArray("assets")
            if (assets != null) {
                for (i in 0 until assets.length()) {
                    val asset = assets.getJSONObject(i)
                    val name = asset.optString("name", "")
                    val downloadUrl = asset.optString("browser_download_url", "")

                    if (name.endsWith(".apk", ignoreCase = true)) {
                        apkUrl = downloadUrl
                        apkName = name
                        break
                    }
                }
            }

            AndroidUpdateInfo(
                currentVersion = currentVersionName,
                latestVersion = cleanLatest,
                isUpdateAvailable = isUpdateAvailable,
                releaseTitle = releaseTitle,
                releaseNotes = releaseNotes,
                downloadUrl = apkUrl,
                fileName = apkName,
                releaseHtmlUrl = htmlUrl
            )
        } catch (e: Exception) {
            null
        }
    }

    private fun isNewerVersion(latest: String, current: String): Boolean {
        return try {
            val latestParts = latest.split(".").map { it.toIntOrNull() ?: 0 }
            val currentParts = current.split(".").map { it.toIntOrNull() ?: 0 }

            val maxLen = maxOf(latestParts.size, currentParts.size)
            for (i in 0 until maxLen) {
                val l = latestParts.getOrElse(i) { 0 }
                val c = currentParts.getOrElse(i) { 0 }
                if (l > c) return true
                if (l < c) return false
            }
            false
        } catch (e: Exception) {
            !latest.equals(current, ignoreCase = true)
        }
    }

    suspend fun downloadApk(
        context: Context,
        downloadUrl: String,
        fileName: String,
        onProgress: (Int) -> Unit
    ): File? = withContext(Dispatchers.IO) {
        try {
            val url = URL(downloadUrl)
            val connection = (url.openConnection() as HttpURLConnection).apply {
                connectTimeout = 15000
                readTimeout = 30000
                instanceFollowRedirects = true
            }

            val totalBytes = connection.contentLength.toLong()
            val updateDir = File(context.cacheDir, "updates").apply { mkdirs() }
            val destination = File(updateDir, if (fileName.isNotBlank()) fileName else "photoforge-update.apk")

            connection.inputStream.use { input ->
                FileOutputStream(destination).use { output ->
                    val buffer = ByteArray(8192)
                    var totalRead = 0L
                    var read: Int

                    while (input.read(buffer).also { read = it } != -1) {
                        output.write(buffer, 0, read)
                        totalRead += read
                        if (totalBytes > 0) {
                            val percent = ((totalRead * 100) / totalBytes).toInt()
                            onProgress(percent)
                        }
                    }
                }
            }
            destination
        } catch (e: Exception) {
            null
        }
    }

    fun installApk(context: Context, apkFile: File) {
        val uri: Uri = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            FileProvider.getUriForFile(
                context,
                "${context.packageName}.fileprovider",
                apkFile
            )
        } else {
            Uri.fromFile(apkFile)
        }

        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        context.startActivity(intent)
    }

    fun openReleaseInBrowser(context: Context, url: String) {
        try {
            val browserIntent = Intent(Intent.ACTION_VIEW, Uri.parse(url)).apply {
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(browserIntent)
        } catch (_: Exception) {}
    }
}
