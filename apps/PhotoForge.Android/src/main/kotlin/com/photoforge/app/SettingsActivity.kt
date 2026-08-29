package com.photoforge.app

import android.os.Bundle
import android.widget.RadioButton
import android.widget.RadioGroup
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity

class SettingsActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        val radioGroup = RadioGroup(this).apply {
            setPadding(32, 32, 32, 32)
        }

        val optExact = RadioButton(this).apply { text = "Keep Exact GPS (5 decimal precision)"; id = 1; isChecked = true }
        val optRound = RadioButton(this).apply { text = "Round GPS (1km privacy blur)"; id = 2 }
        val optRemove = RadioButton(this).apply { text = "Completely Strip GPS metadata"; id = 3 }
        val optWarn = RadioButton(this).apply { text = "Copy with warning"; id = 4 }

        radioGroup.addView(optExact)
        radioGroup.addView(optRound)
        radioGroup.addView(optRemove)
        radioGroup.addView(optWarn)

        radioGroup.setOnCheckedChangeListener { _, checkedId ->
            val policy = when (checkedId) {
                1 -> "Keep Exact"
                2 -> "Round (1km)"
                3 -> "Remove"
                4 -> "CopyWithWarning"
                else -> "Keep Exact"
            }
            Toast.makeText(this, "GPS Privacy Policy set to: $policy", Toast.LENGTH_SHORT).show()
        }

        setContentView(radioGroup)
    }
}
