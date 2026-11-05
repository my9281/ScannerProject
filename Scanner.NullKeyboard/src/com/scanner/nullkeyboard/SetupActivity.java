package com.scanner.nullkeyboard;

import android.app.Activity;
import android.os.Bundle;
import android.content.Intent;
import android.provider.Settings;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

public final class SetupActivity extends Activity {
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        int padding = (int) (24 * getResources().getDisplayMetrics().density);
        layout.setPadding(padding, padding, padding, padding);
        TextView text = new TextView(this);
        text.setText("空白输入法 1.1\n\n用于 PDA 的单行数字键盘，高度 36dp。\n只有 1 2 3 4 5 6 7 8 9 0，无候选栏、无联网权限。\n\n1. 在系统设置中启用“空白输入法”。\n2. 切换到“空白输入法”。\n\n需要其他输入时，可随时回到这里切换回原来的输入法。\n");
        text.setTextSize(18);
        layout.addView(text);
        Button enable = new Button(this);
        enable.setText("1. 打开输入法设置");
        enable.setOnClickListener(v -> startActivity(new Intent(Settings.ACTION_INPUT_METHOD_SETTINGS)));
        layout.addView(enable);
        Button choose = new Button(this);
        choose.setText("2. 选择 / 切换输入法");
        choose.setOnClickListener(v -> ((InputMethodManager) getSystemService(INPUT_METHOD_SERVICE)).showInputMethodPicker());
        layout.addView(choose);
        setContentView(layout);
    }
}
