package com.photoforge.app.storage

import android.content.Context
import android.content.SharedPreferences
import com.photoforge.app.model.ConversionQuality
import com.photoforge.app.model.GpsPrivacyPolicy

class PreferencesManager(context: Context) {

    private val prefs: SharedPreferences =
        context.getSharedPreferences("photoforge_preferences", Context.MODE_PRIVATE)

    var gpsPrivacyPolicy: GpsPrivacyPolicy
        get() {
            val name = prefs.getString(KEY_GPS_POLICY, GpsPrivacyPolicy.KEEP_EXACT.name)
            return try {
                GpsPrivacyPolicy.valueOf(name ?: GpsPrivacyPolicy.KEEP_EXACT.name)
            } catch (e: Exception) {
                GpsPrivacyPolicy.KEEP_EXACT
            }
        }
        set(value) {
            prefs.edit().putString(KEY_GPS_POLICY, value.name).apply()
        }

    var defaultConversionQuality: ConversionQuality
        get() {
            val name = prefs.getString(KEY_QUALITY, ConversionQuality.HIGH.name)
            return try {
                ConversionQuality.valueOf(name ?: ConversionQuality.HIGH.name)
            } catch (e: Exception) {
                ConversionQuality.HIGH
            }
        }
        set(value) {
            prefs.edit().putString(KEY_QUALITY, value.name).apply()
        }

    var defaultExportFormat: String
        get() = prefs.getString(KEY_FORMAT, "KEEP") ?: "KEEP"
        set(value) {
            prefs.edit().putString(KEY_FORMAT, value).apply()
        }

    var autoAcceptConfidentMatches: Boolean
        get() = prefs.getBoolean(KEY_AUTO_ACCEPT, true)
        set(value) {
            prefs.edit().putBoolean(KEY_AUTO_ACCEPT, value).apply()
        }

    var preserveKeywords: Boolean
        get() = prefs.getBoolean(KEY_PRESERVE_KEYWORDS, true)
        set(value) {
            prefs.edit().putBoolean(KEY_PRESERVE_KEYWORDS, value).apply()
        }

    var autoCheckUpdates: Boolean
        get() = prefs.getBoolean(KEY_AUTO_CHECK_UPDATES, true)
        set(value) {
            prefs.edit().putBoolean(KEY_AUTO_CHECK_UPDATES, value).apply()
        }

    companion object {
        private const val KEY_GPS_POLICY = "pref_gps_policy"
        private const val KEY_QUALITY = "pref_quality"
        private const val KEY_FORMAT = "pref_format"
        private const val KEY_AUTO_ACCEPT = "pref_auto_accept"
        private const val KEY_PRESERVE_KEYWORDS = "pref_preserve_keywords"
        private const val KEY_AUTO_CHECK_UPDATES = "pref_auto_check_updates"
    }
}
