package com.photoforge.app

import android.os.Bundle
import android.widget.LinearLayout
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity

class MatchReviewActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(32, 32, 32, 32)
        }

        val title = TextView(this).apply {
            text = "Candidate Match Review"
            textSize = 20f
        }

        val subtitle = TextView(this).apply {
            text = "Review top candidate original images ranked by multi-signal score"
            textSize = 14f
        }

        layout.addView(title)
        layout.addView(subtitle)
        setContentView(layout)
    }
}
