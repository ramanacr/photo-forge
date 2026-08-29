package com.photoforge.app

import android.os.Bundle
import android.widget.LinearLayout
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity

class DiffInspectorActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(32, 32, 32, 32)
        }

        val title = TextView(this).apply {
            text = "Metadata Diff Inspector"
            textSize = 20f
        }

        val subtitle = TextView(this).apply {
            text = "Inspect provenance tags copied from original vs preserved from edited"
            textSize = 14f
        }

        layout.addView(title)
        layout.addView(subtitle)
        setContentView(layout)
    }
}
