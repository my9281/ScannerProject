package com.printer.example.activity;

import android.app.Activity;
import android.os.Bundle;
import android.support.v7.app.AppCompatActivity;
import android.text.TextUtils;
import android.view.View;
import android.widget.EditText;

import com.printer.example.R;
import com.printer.example.app.BaseApplication;
import com.rt.printerlibrary.cmd.Cmd;
import com.rt.printerlibrary.cmd.EscFactory;
import com.rt.printerlibrary.factory.cmd.CmdFactory;
import com.rt.printerlibrary.printer.RTPrinter;
import com.rt.printerlibrary.setting.CommonSetting;

public class SetXYActivity extends Activity {
    private EditText etv_plus,etv_sub,etv_poitx,etv_poity;
    private RTPrinter rtPrinter;
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_set_x_y);
        initview();
        rtPrinter = BaseApplication.getInstance().getRtPrinter();
    }

    private void initview() {
        etv_plus= findViewById(R.id.etv_plus);
        etv_sub= findViewById(R.id.etv_sub);
        etv_poitx= findViewById(R.id.etv_poitx);
        etv_poity= findViewById(R.id.etv_poity);
    }
    public void onBtnClick(View v) {
        switch (v.getId()) {
            case R.id.btn_plus:
                String plus = etv_plus.getText().toString();
                if(TextUtils.isEmpty(plus))return;
                blackPlus(plus);
                break;
            case R.id.btn_sub:
                String sub = etv_sub.getText().toString();
                if(TextUtils.isEmpty(sub))return;
                blackSub(sub);
                break;
            case R.id.btn_pointxy:
                String x = etv_poitx.getText().toString();
                if(TextUtils.isEmpty(x))return;
                String y = etv_poity.getText().toString();
                if(TextUtils.isEmpty(x))return;
                pointxy(x,y);
                break;
            default:
                break;
        }
    }

    private void blackPlus(String plus) {
        CmdFactory cmdFactory = new EscFactory();
        Cmd cmd = cmdFactory.create();
        CommonSetting commonSetting = new CommonSetting();
        byte[] plus1 = commonSetting.setBlackLabelPlus(Integer.valueOf(plus));
        cmd.append(plus1);
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }
    private void blackSub(String sub) {
        CmdFactory cmdFactory = new EscFactory();
        Cmd cmd = cmdFactory.create();
        CommonSetting commonSetting = new CommonSetting();
        byte[] sub1 = commonSetting.setBlackLabelSub(Integer.valueOf(sub));
        cmd.append(sub1);
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }
    private void pointxy(String x, String y) {
        CmdFactory cmdFactory = new EscFactory();
        Cmd cmd = cmdFactory.create();
        CommonSetting commonSetting = new CommonSetting();
        byte[] xy = commonSetting.setPointXY(Integer.valueOf(x),Integer.valueOf(y));
        cmd.append(xy);
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }

}