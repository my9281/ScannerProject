package com.scanner.nullkeyboard;

import android.inputmethodservice.InputMethodService;
import android.graphics.Color;
import android.os.Build;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputConnection;
import android.widget.LinearLayout;
import android.widget.TextView;

/** A single compact digit row; physical scanner events retain platform handling. */
public final class NullKeyboardService extends InputMethodService {
    private int rowHeight() {
        return Math.round(36 * getResources().getDisplayMetrics().density);
    }

    @Override public View onCreateInputView() {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setLayoutDirection(View.LAYOUT_DIRECTION_LTR);
        row.setPadding(0, 0, 0, 0);
        row.setMinimumHeight(0);
        row.setBackgroundColor(Color.rgb(238, 240, 243));
        row.setLayoutParams(new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, rowHeight()));
        for (char digit : "1234567890".toCharArray()) {
            final String value = String.valueOf(digit);
            TextView key = new TextView(this);
            key.setText(value);
            key.setTextSize(18);
            key.setTextColor(Color.rgb(25, 30, 36));
            key.setGravity(Gravity.CENTER);
            key.setPadding(0, 0, 0, 0);
            key.setMinimumHeight(0);
            key.setContentDescription(value);
            android.util.TypedValue highlight = new android.util.TypedValue();
            getTheme().resolveAttribute(android.R.attr.selectableItemBackground, highlight, true);
            if (highlight.resourceId != 0) key.setBackgroundResource(highlight.resourceId);
            key.setOnClickListener(v -> {
                InputConnection connection = getCurrentInputConnection();
                if (connection != null) connection.commitText(value, 1);
            });
            row.addView(key, new LinearLayout.LayoutParams(0, rowHeight(), 1));
        }
        return row;
    }

    @Override public void onStartInputView(EditorInfo info, boolean restarting) {
        super.onStartInputView(info, restarting);
        setCandidatesViewShown(false);
        setExtractViewShown(false);
        // The framework's input container must wrap the row, without a minimum height.
        View inputArea = getWindow().getWindow().getDecorView()
                .findViewById(android.R.id.inputArea);
        if (inputArea != null) {
            inputArea.setMinimumHeight(0);
            inputArea.setPadding(0, 0, 0, 0);
        }
        if (Build.VERSION.SDK_INT >= 29) {
            getWindow().getWindow().setNavigationBarContrastEnforced(false);
        }
    }

    @Override public boolean onEvaluateInputViewShown() { return true; }
    @Override public boolean onEvaluateFullscreenMode() { return false; }
    @Override public boolean onShowInputRequested(int flags, boolean configChange) { return true; }
}
