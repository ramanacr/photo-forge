package com.photoforge.app

import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import com.photoforge.app.databinding.ActivitySettingsBinding
import com.photoforge.app.model.GpsPrivacyPolicy
import com.photoforge.app.storage.PreferencesManager

class SettingsActivity : AppCompatActivity() {

    private lateinit var binding: ActivitySettingsBinding
    private lateinit var prefsManager: PreferencesManager

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySettingsBinding.inflate(layoutInflater)
        setContentView(binding.root)
        prefsManager = PreferencesManager(this)

        binding.toolbar.setNavigationOnClickListener { finish() }

        loadSettings()

        binding.rgGpsPolicy.setOnCheckedChangeListener { _, checkedId ->
            val policy = when (checkedId) {
                R.id.rbGpsExact -> GpsPrivacyPolicy.KEEP_EXACT
                R.id.rbGpsRound -> GpsPrivacyPolicy.ROUND
                R.id.rbGpsRemove -> GpsPrivacyPolicy.REMOVE
                R.id.rbGpsWarning -> GpsPrivacyPolicy.COPY_WITH_WARNING
                else -> GpsPrivacyPolicy.KEEP_EXACT
            }
            prefsManager.gpsPrivacyPolicy = policy
            Toast.makeText(this, "GPS Privacy set to: ${policy.title}", Toast.LENGTH_SHORT).show()
        }

        binding.swAutoAccept.setOnCheckedChangeListener { _, isChecked ->
            prefsManager.autoAcceptConfidentMatches = isChecked
        }

        binding.swPreserveKeywords.setOnCheckedChangeListener { _, isChecked ->
            prefsManager.preserveKeywords = isChecked
        }

        binding.swAutoCheckUpdates.setOnCheckedChangeListener { _, isChecked ->
            prefsManager.autoCheckUpdates = isChecked
        }

        binding.btnCheckUpdates.setOnClickListener {
            performUpdateCheck()
        }
    }

    private val updateEngine = com.photoforge.app.engine.AndroidUpdateEngine()

    private fun getAppVersionName(): String {
        return try {
            val pInfo = if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
                packageManager.getPackageInfo(packageName, android.content.pm.PackageManager.PackageInfoFlags.of(0))
            } else {
                @Suppress("DEPRECATION")
                packageManager.getPackageInfo(packageName, 0)
            }
            pInfo.versionName ?: "1.3.0"
        } catch (_: Exception) {
            "1.3.0"
        }
    }

    private fun performUpdateCheck() {
        lifecycleScope.launch {
            try {
                binding.btnCheckUpdates.isEnabled = false
                binding.tvUpdateStatus.text = "Checking GitHub Releases..."
                binding.prgUpdateProgress.visibility = android.view.View.GONE

                val currentVer = getAppVersionName()
                val update = updateEngine.checkForUpdates(currentVer)

                if (update == null) {
                    binding.tvUpdateStatus.text = "Unable to reach GitHub Releases. Check network connection."
                    binding.btnCheckUpdates.isEnabled = true
                    return@launch
                }

                if (!update.isUpdateAvailable) {
                    binding.tvUpdateStatus.text = "✔ PhotoForge is up to date (v$currentVer)."
                    Toast.makeText(this@SettingsActivity, "You are using the latest version (v$currentVer)", Toast.LENGTH_SHORT).show()
                    binding.btnCheckUpdates.isEnabled = true
                    return@launch
                }

                binding.tvUpdateStatus.text = "🌟 New version v${update.latestVersion} available!"
                showUpdateDialog(update)
            } catch (e: Exception) {
                binding.tvUpdateStatus.text = "Update check error: ${e.message}"
            } finally {
                binding.btnCheckUpdates.isEnabled = true
            }
        }
    }

    private fun showUpdateDialog(update: com.photoforge.app.engine.AndroidUpdateInfo) {
        val notes = if (update.releaseNotes.isNotBlank()) update.releaseNotes else "Bug fixes and performance improvements."
        val message = "A new version of PhotoForge is available!\n\n" +
                "• Installed: v${update.currentVersion}\n" +
                "• Latest: v${update.latestVersion}\n\n" +
                "Release Notes:\n$notes"

        val builder = androidx.appcompat.app.AlertDialog.Builder(this)
            .setTitle("🚀 Update Available: v${update.latestVersion}")
            .setMessage(message)

        if (update.downloadUrl.isNotBlank()) {
            builder.setPositiveButton("Download & Install") { _, _ ->
                startDownloadAndInstall(update)
            }
            builder.setNeutralButton("View on GitHub") { _, _ ->
                updateEngine.openReleaseInBrowser(this, update.releaseHtmlUrl)
            }
        } else {
            builder.setPositiveButton("View on GitHub") { _, _ ->
                updateEngine.openReleaseInBrowser(this, update.releaseHtmlUrl)
            }
        }

        builder.setNegativeButton("Later", null)
        builder.show()
    }

    private fun startDownloadAndInstall(update: com.photoforge.app.engine.AndroidUpdateInfo) {
        lifecycleScope.launch {
            try {
                binding.btnCheckUpdates.isEnabled = false
                binding.prgUpdateProgress.visibility = android.view.View.VISIBLE
                binding.prgUpdateProgress.progress = 0
                binding.tvUpdateStatus.text = "Downloading v${update.latestVersion} APK..."

                val apkFile = updateEngine.downloadApk(
                    context = this@SettingsActivity,
                    downloadUrl = update.downloadUrl,
                    fileName = update.fileName
                ) { percent ->
                    runOnUiThread {
                        binding.prgUpdateProgress.progress = percent
                        binding.tvUpdateStatus.text = "Downloading v${update.latestVersion} APK: $percent%"
                    }
                }

                if (apkFile != null && apkFile.exists()) {
                    binding.tvUpdateStatus.text = "✔ Download complete! Launching package installer..."
                    updateEngine.installApk(this@SettingsActivity, apkFile)
                } else {
                    binding.tvUpdateStatus.text = "Download failed. Redirecting to GitHub..."
                    updateEngine.openReleaseInBrowser(this@SettingsActivity, update.releaseHtmlUrl)
                }
            } catch (e: Exception) {
                binding.tvUpdateStatus.text = "Download error: ${e.message}"
                Toast.makeText(this@SettingsActivity, "Download failed: ${e.message}", Toast.LENGTH_LONG).show()
            } finally {
                binding.prgUpdateProgress.visibility = android.view.View.GONE
                binding.btnCheckUpdates.isEnabled = true
            }
        }
    }

    private fun loadSettings() {
        when (prefsManager.gpsPrivacyPolicy) {
            GpsPrivacyPolicy.KEEP_EXACT -> binding.rbGpsExact.isChecked = true
            GpsPrivacyPolicy.ROUND -> binding.rbGpsRound.isChecked = true
            GpsPrivacyPolicy.REMOVE -> binding.rbGpsRemove.isChecked = true
            GpsPrivacyPolicy.COPY_WITH_WARNING -> binding.rbGpsWarning.isChecked = true
        }

        binding.swAutoAccept.isChecked = prefsManager.autoAcceptConfidentMatches
        binding.swPreserveKeywords.isChecked = prefsManager.preserveKeywords
        binding.swAutoCheckUpdates.isChecked = prefsManager.autoCheckUpdates
        binding.tvCurrentVersion.text = "v${getAppVersionName()}"
    }
}
