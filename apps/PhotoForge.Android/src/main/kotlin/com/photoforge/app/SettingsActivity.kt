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
    }
}
