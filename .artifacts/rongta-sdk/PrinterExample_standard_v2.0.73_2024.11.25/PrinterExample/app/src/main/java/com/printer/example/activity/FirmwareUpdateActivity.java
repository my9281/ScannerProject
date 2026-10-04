package com.printer.example.activity;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.ContentResolver;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.FileUtils;
import android.provider.OpenableColumns;
import android.support.annotation.RequiresApi;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

import com.printer.example.R;
import com.printer.example.app.BaseApplication;
import com.printer.example.utils.LogUtils;
import com.printer.example.utils.ToastUtil;
import com.rt.printerlibrary.printer.RTPrinter;
import com.rt.printerlibrary.utils.UpdateUtils;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;


public class FirmwareUpdateActivity extends Activity implements View.OnClickListener{
    public static final String SP_NAME_SETTING = "setting";
    private LinearLayout firmware_back;
    private TextView tv_filepath, tv_progress;
    private Button btn_browse, btn_update, btn_reset;
    private ProgressBar pb_update;

    private String path = "";
    private StringBuffer stringBuffer = new StringBuffer("");
    private final static int UPDATE_PROGRESS = 22;
    private boolean isCanSendNext = false;
    private static int pkgNum = 0;
    private ArrayList<byte[]> list;
    private UIUpdateRunnable uiUpdateRunnable = null;
    private RTPrinter rtPrinter;

    private boolean isUpdate;
    private UpdateUtils updateUtils;
    public SharedPreferences sp;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_firmware_update);
        initView();
        initData();
        addListener();
    }

    private void initData() {
       sp = getSharedPreferences(SP_NAME_SETTING, Context.MODE_PRIVATE);
       path=sp.getString("updatebinfilepath","");
       tv_filepath.setText(path);
        uiUpdateRunnable = new UIUpdateRunnable();
        rtPrinter = BaseApplication.getInstance().getRtPrinter();;
        updateUtils = new UpdateUtils();
        updateUtils.setPkgNum(2);
        updateUtils.setUpdataTimeInval(50);
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
    }

    @Override
    protected void onPause() {
        super.onPause();
    }

    private void initView() {
        firmware_back = findViewById(R.id.firmware_back);
        tv_filepath = findViewById(R.id.tv_filepath);
        tv_progress = findViewById(R.id.tv_progress);
        btn_browse = findViewById(R.id.btn_browse);
        btn_update = findViewById(R.id.btn_update);
        btn_reset = findViewById(R.id.btn_reset);
        pb_update = findViewById(R.id.pb_update);
        tv_filepath.setText(path);
        tv_progress.setVisibility(View.INVISIBLE);
    }


    private void addListener() {
        firmware_back.setOnClickListener(this);
        btn_browse.setOnClickListener(this);
        btn_update.setOnClickListener(this);
        btn_reset.setOnClickListener(this);
    }

    private void openFileBrowseWindow() {
        Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[]{
                "application/octet-stream"
        });
        startActivityForResult(intent, 2);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == 2) {
            if (data == null) return;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                Uri uri = data.getData();
                if (uri != null) {
                    File file = uriToFileApiQ(uri, this);
                    if (file != null) {
                        if (file.exists()) {
                            path = file.toString();
                            tv_filepath.setText(path);
                            Log.i("rongtaPrint", "onActivityResult: path=" + path);
                        }
                    }
                }
            }else {
                if (data.getStringExtra("path") != null) {
                    path = data.getStringExtra("path");
                    tv_filepath.setText(path);
                    sp.edit().putString("updatebinfilepath", path).apply();
//                    ArrayList<byte[]> firmwareUpdateCDM = updateUtils.getFirmwareUpdateCmd(path, 2);//固件路径path 下发包数数，默认为2
//                    for (int i = 0; i < firmwareUpdateCDM.size(); i++) {
//                        rtPrinter.writeMsgAsync(firmwareUpdateCDM.get(i));
//                        try {
//                            Thread.sleep(50);
//                        } catch (InterruptedException e) {
//                            throw new RuntimeException(e);
//                        }
//                    }
                }
            }
        }
    }
    @RequiresApi(api = Build.VERSION_CODES.Q)
    @SuppressLint("Range")
    public File uriToFileApiQ(Uri uri, Activity activity) {
        File file =null;
        //android10以上转换
        if (uri.getScheme().equals(ContentResolver.SCHEME_FILE)) {
            file =new File(uri.getPath());
        }else if (uri.getScheme().equals(ContentResolver.SCHEME_CONTENT)) {
            //把文件复制到沙盒目录
            ContentResolver contentResolver =activity.getContentResolver();
            Cursor cursor =contentResolver.query(uri,null,null,null,null);
            if (cursor.moveToFirst()) {
                String displayName =cursor.getString(cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME));
                try {
                    InputStream is =contentResolver.openInputStream(uri);
                    File cache =new File(activity.getExternalCacheDir().getAbsolutePath(),Math.round((Math.random() +1) *1000) +displayName);
                    FileOutputStream fos =new FileOutputStream(cache);
                    FileUtils.copy(is,fos);
                    file =cache;
                    fos.close();
                    is.close();
                }catch (IOException e) {
                    e.printStackTrace();
                }
            }
        }
        return file;
    }


    private void startUpdate() {
        if (isUpdate) {
            return;
        }
        isUpdate = true;
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    pbDisplay();
                    updateUtils.sendFileToPrinter(rtPrinter, path, new UpdateUtils.OnUpdateListener() {
                        @Override
                        public void onUpdate(int curProgress, int max) {//更新进度条 curProgress当前进度 max 固件bin总长度
                            uiUpdateRunnable.setCurProgress(curProgress);
                            uiUpdateRunnable.setMax(max);
                            runOnUiThread(uiUpdateRunnable);
                        }

                        @Override
                        public void onFinish() {
                            LogUtils.d("rongtaPrint","Update Finish...");
                            toastMsg(getString(R.string.update_finish));
                        }

                        @Override
                        public void onError() {
                            isUpdate = false;
                            LogUtils.e("rongtaPrint:", "Update Erro");
                            toastMsg("Update Erro");
                        }
                    });
                } catch (Exception e) {
                    e.printStackTrace();
                } finally {
                    isUpdate = false;

                }
            }
        }).start();
    }

    @Override
    public void onClick(View v) {
        switch (v.getId()) {
            case R.id.firmware_back:
                finish();
                break;
            case R.id.btn_browse:
                openFileBrowseWindow();
                break;
            case R.id.btn_update:
                checkIsCanUpdate();
                break;
            case R.id.btn_reset:
                pkgNum = 0;
                list = null;
                path = "";
                tv_filepath.setText(path);
                uiUpdateRunnable.setMax(100);
                uiUpdateRunnable.setCurProgress(0);
                runOnUiThread(uiUpdateRunnable);
                break;
            default:
                break;
        }
    }

    private void checkIsCanUpdate() {
        if (TextUtils.isEmpty(path)) {//Dosen't choose a update file
            ToastUtil.showLong(FirmwareUpdateActivity.this, getString(R.string.tip_choosefile));
            return;
        }
        AlertDialog.Builder builder = new AlertDialog.Builder(FirmwareUpdateActivity.this);
        builder.setTitle(getString(R.string.str_firmware_update))
                .setMessage(R.string.dialog_sure_update)
                .setPositiveButton(R.string.dialog_ok, new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        startUpdate();
                    }
                }).setNegativeButton(R.string.dialog_cancel, null);
        builder.show();
    }

    private void pbDisplay() {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                pb_update.setVisibility(View.VISIBLE);
                tv_progress.setVisibility(View.VISIBLE);
            }
        });
    }
   private void toastMsg(final String msg){
       runOnUiThread(new Runnable() {
           @Override
           public void run() {
               ToastUtil.showLong(FirmwareUpdateActivity.this, msg);
           }
       });
   }


    private class UIUpdateRunnable implements Runnable {
        private int curProgress = 0, max;
        public int getCurProgress() {
            return curProgress;
        }
        public void setCurProgress(int curProgress) {
            this.curProgress = curProgress;
        }
        public int getMax() {
            return max;
        }
        public void setMax(int max) {
            this.max = max;
        }

        @SuppressLint("SetTextI18n")
        @Override
        public void run() {
            if (pb_update == null || tv_progress == null) {
                return;
            }
            int percent = curProgress * 100 / max;
            pb_update.setProgress(percent);
            tv_progress.setText(pb_update.getProgress() + "%");
            if(percent==100) {
                toastMsg(getString(R.string.tip_updata_successful_restart_printer));
                try {
                    Thread.sleep(5000);
                    BaseApplication.getInstance().getRtPrinter().disConnect();
                } catch (InterruptedException e) {
                    throw new RuntimeException(e);
                }
            }
        }
    }

}
