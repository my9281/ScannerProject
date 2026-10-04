package com.printer.example.activity;

import android.app.ProgressDialog;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.net.Uri;
import android.os.Bundle;
import android.provider.MediaStore;
import android.util.Log;
import android.view.View;
import android.widget.AdapterView;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.SimpleCursorTreeAdapter;

import com.printer.example.R;
import com.printer.example.app.BaseActivity;
import com.printer.example.app.BaseApplication;
import com.printer.example.utils.BaseEnum;
import com.printer.example.utils.LogUtils;
import com.printer.example.utils.SaveMediaFileUtil;
import com.printer.example.utils.ToastUtil;
import com.printer.example.view.BasicListDialog;
import com.rt.printerlibrary.bean.LableSizeBean;
import com.rt.printerlibrary.bean.Position;
import com.rt.printerlibrary.cmd.Cmd;
import com.rt.printerlibrary.cmd.CpclFactory;
import com.rt.printerlibrary.cmd.EscCmd;
import com.rt.printerlibrary.cmd.EscFactory;
import com.rt.printerlibrary.cmd.PinFactory;
import com.rt.printerlibrary.cmd.TscFactory;
import com.rt.printerlibrary.cmd.ZplFactory;
import com.rt.printerlibrary.enumerate.BmpPrintMode;
import com.rt.printerlibrary.enumerate.CommonEnum;
import com.rt.printerlibrary.enumerate.PrintDirection;
import com.rt.printerlibrary.exception.SdkException;
import com.rt.printerlibrary.factory.cmd.CmdFactory;
import com.rt.printerlibrary.printer.RTPrinter;
import com.rt.printerlibrary.setting.BitmapSetting;
import com.rt.printerlibrary.setting.CommonSetting;
import com.rt.printerlibrary.utils.BitmapConvertUtil;
import com.rt.printerlibrary.utils.BitmapUtil;
import com.rt.printerlibrary.utils.FuncUtils;
import com.rt.printerlibrary.utils.PrintStatusCmd;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;

public class ImagePrintActivity extends BaseActivity implements View.OnClickListener {

    private static final int CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE = 0xd0;
    private static final int ALBUM_IMAGE_ACTIVITY_REQUEST_CODE = 0xd1;

    private LinearLayout llUploadImage;
    private Button btn_print,btn_nv_define,btn_nv_print,btn_rotate;
    private FrameLayout flContent;
    private ImageView ivImage;
    private EditText et_pic_width,et_pic_height;

    private RTPrinter rtPrinter;
    private Uri imageUri;
    private Bitmap mBitmap;
    private int lablelWidth = 80;
    private int lablelHeight = 150;
    private int bmpPrintWidth = 67;
    private int bmpPrintHeight = 150;
    ProgressDialog progressDialog;


    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_image_print);
        initView();
        addListener();
        init();
    }

    @Override
    public void initView() {
        llUploadImage = findViewById(R.id.ll_print_image_upload_image);
        btn_print = findViewById(R.id.print);
        flContent = findViewById(R.id.fl_print_image_content);
        ivImage = findViewById(R.id.iv_print_image_image);
        et_pic_width = findViewById(R.id.et_pic_width);
        et_pic_height = findViewById(R.id.et_pic_height);
        btn_nv_define = findViewById(R.id.btn_nv_define);
        btn_nv_print = findViewById(R.id.btn_nv_print);
        btn_rotate = findViewById(R.id.btn_rotate);
        btn_nv_define.setOnClickListener(this);
        btn_nv_print.setOnClickListener(this);
        btn_rotate.setOnClickListener(this);
        if(BaseApplication.getInstance().getCurrentCmdType()==BaseEnum.CMD_ESC){
            btn_nv_define.setVisibility(View.VISIBLE);
            btn_nv_print.setVisibility(View.VISIBLE);
        }else {
            btn_nv_define.setVisibility(View.GONE);
            btn_nv_print.setVisibility(View.GONE);
        }
    }

    @Override
    public void init() {
        rtPrinter = BaseApplication.getInstance().getRtPrinter();
        et_pic_width.setText(bmpPrintWidth+"");
        et_pic_height.setText(bmpPrintHeight+"");
    }
    BitmapUtil bitmapUtil;
    @Override
    public void addListener() {
        llUploadImage.setOnClickListener(this);
        btn_print.setOnClickListener(this);
        progressDialog=new ProgressDialog(this);
        bitmapUtil=new BitmapUtil();
    }
    private boolean isDefine=false;
    @Override
    public void onClick(View view) {
        switch (view.getId()) {
            case R.id.btn_rotate:
                rotateBm();
                break;
            case R.id.print:
                print();
                break;
            case R.id.ll_print_image_upload_image:
            case R.id.fl_print_image_content:
                showPictureModeChooseDialog();
                break;
            case R.id.btn_nv_define:
                isDefine = false;
                byte[] escNvCmdList = bitmapUtil.getESCNVBitmapCmd(mBitmap, 1);//defined NV logo
                if (escNvCmdList.length == 0) {
                    ToastUtil.show(ImagePrintActivity.this, "NV logo exceeds the maximum value");
                } else {
                    isDefine = true;
                    if (rtPrinter != null) {
                        rtPrinter.writeMsgAsync(escNvCmdList);
                    }
                }
                break;
            case R.id.btn_nv_print:
                if (!isDefine)
                    ToastUtil.show(ImagePrintActivity.this, "NV logo needs to be defined first");
                else {
                    if (rtPrinter != null) {
                        rtPrinter.writeMsgAsync(bitmapUtil.getEscNvCmdPrint(1));//print NV logo
                        CmdFactory cmdFactory = new EscFactory();
                        Cmd cmd = cmdFactory.create();
                        cmd.append(cmd.getLFCRCmd());
                        cmd.append(cmd.getLFCRCmd());
                        cmd.append(cmd.getLFCRCmd());
                        cmd.append(cmd.getLFCRCmd());
                        rtPrinter.writeMsgAsync(cmd.getAppendCmds());//FEED
                    }
                }
                break;
            default:
                break;
        }
    }
    private int rotateDegree=90;
    private void rotateBm() {
        Matrix matrix = new Matrix(); // 创建操作图片用的矩阵对象
        matrix.postRotate(rotateDegree); // 执行图片的旋转动作
        // 创建并返回旋转后的位图对象
        mBitmap =  Bitmap.createBitmap(mBitmap, 0, 0, mBitmap.getWidth(),
                mBitmap.getHeight(), matrix, false);
        ivImage.setImageBitmap(mBitmap);
    }


    private void print() {
        if (mBitmap == null) {//未选择图片
            ToastUtil.show(this, R.string.tip_upload_image);
            return;
        }
        try {
            bmpPrintWidth = Integer.parseInt(et_pic_width.getText().toString());
            bmpPrintHeight = Integer.parseInt(et_pic_height.getText().toString());
        } catch (Exception e) {
            e.printStackTrace();
        }
        switch (BaseApplication.getInstance().getCurrentCmdType()) {
            case BaseEnum.CMD_PIN:
                pinPrint();
                break;
            case BaseEnum.CMD_ESC:
                escPrint();
                break;
            case BaseEnum.CMD_TSC:
                tscPrint();
                break;
            case BaseEnum.CMD_CPCL:
                cpclPrint();
                break;
            case BaseEnum.CMD_ZPL:
                zplPrint();
                break;
            default:
                break;
        }
    }
    private void cpclPrint() {
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    showProgressDialog(null);
                    CmdFactory cpclFactory = new CpclFactory();
                    Cmd cmd = cpclFactory.create();
                    /* //BQ20240329374F041 韩国RPP30项目修改
                    cmd.append(cmd.getCpclHeaderCmd(lablelWidth, lablelHeight, 1, 0));
                    BitmapSetting bitmapSetting = new BitmapSetting();
                    bitmapSetting.setPrintPostion(new Position(24, 8));//起始坐标偏移
                    bitmapSetting.setBimtapLimitWidth(bmpPrintWidth * 8);
                    bitmapSetting.setBimtapLimitHeight(bmpPrintHeight * 8);
                    bitmapSetting.setBmpPrintMode(BmpPrintMode.MODE_SINGLE_FAST);*/
                    cmd.append(cmd.getCpclHeaderCmd(bmpPrintWidth, bmpPrintHeight, 1, 0));
                    BitmapSetting bitmapSetting = new BitmapSetting();
                    bitmapSetting.setPrintPostion(new Position(0, 0));
                    bitmapSetting.setBimtapLimitWidth(bmpPrintWidth * 8);
                    bitmapSetting.setBimtapLimitHeight(bmpPrintHeight * 8);
                    bitmapSetting.setBmpPrintMode(BmpPrintMode.MODE_MULTI_COLOR);
                    cmd.append(cmd.getBitmapCmd(bitmapSetting, mBitmap));
                    cmd.append(cmd.getEndCmd());
                    if (rtPrinter != null) {
                        rtPrinter.writeMsg(cmd.getAppendCmds());
//                    Log.i(TAG, "run: "+FuncUtils.ByteArrToHex(cmd.getAppendCmds()));
                    }
                } catch (SdkException e) {
                    e.printStackTrace();
                }
                hideProgressDialog();
            }
        }).start();
    }


    private void tscPrint(){
        new Thread(new Runnable() {
            @Override
            public void run() {
                showProgressDialog(null);
                CmdFactory tscFac = new TscFactory();
                Cmd tscCmd = tscFac.create();
                tscCmd.append(tscCmd.getHeaderCmd());
                CommonSetting commonSetting = new CommonSetting();
                commonSetting.setLableSizeBean(new LableSizeBean(bmpPrintWidth, bmpPrintHeight));
                commonSetting.setLabelGap(3);
                commonSetting.setPrintDirection(PrintDirection.NORMAL);
                tscCmd.append(tscCmd.getHeaderCmd());
                tscCmd.append(tscCmd.getCommonSettingCmd(commonSetting));
                BitmapSetting bitmapSetting = new BitmapSetting();
                bitmapSetting.setPrintPostion(new Position(0, 0));
                bitmapSetting.setBimtapLimitWidth(bmpPrintWidth * 8);
                bitmapSetting.setBmpPrintMode(BmpPrintMode.MODE_SINGLE_FAST);//单色
//                bitmapSetting.setBmpPrintMode(BmpPrintMode.MODE_MULTI_COLOR);//灰度
                try {
                    tscCmd.append(tscCmd.getBitmapCmd(bitmapSetting, mBitmap));
                    tscCmd.append(tscCmd.getPrintCopies(1));//Print Copies setting
                } catch (SdkException e) {
                    e.printStackTrace();
                }
                if (rtPrinter != null) {
                    rtPrinter.writeMsg(tscCmd.getAppendCmds());
                }
                hideProgressDialog();
            }
        }).start();

    }

    private void zplPrint() {
        new Thread(new Runnable() {
            @Override
            public void run() {
                showProgressDialog(null);
                CmdFactory zplFac = new ZplFactory();
                Cmd zplCmd = zplFac.create();
                zplCmd.append(zplCmd.getHeaderCmd());
                CommonSetting commonSetting = new CommonSetting();
                commonSetting.setLableSizeBean(new LableSizeBean(bmpPrintWidth, bmpPrintHeight));
//                commonSetting.setLabelGap(3);
                commonSetting.setPrintDirection(PrintDirection.NORMAL);
                zplCmd.append(zplCmd.getHeaderCmd());
                zplCmd.append(zplCmd.getCommonSettingCmd(commonSetting));
                BitmapSetting bitmapSetting = new BitmapSetting();
                bitmapSetting.setPrintPostion(new Position(0, 0));
                bitmapSetting.setBimtapLimitWidth(bmpPrintWidth * 8);
                try {
                    zplCmd.append(zplCmd.getBitmapCmd(bitmapSetting, mBitmap));
                    zplCmd.append(zplCmd.getPrintCopies(1));
                } catch (SdkException e) {
                    e.printStackTrace();
                }
                zplCmd.append(zplCmd.getEndCmd());
                if (rtPrinter != null) {
                    rtPrinter.writeMsg(zplCmd.getAppendCmds());
                }
                hideProgressDialog();
            }
        }).start();

    }

    private void escPrint(){
        new Thread(new Runnable() {
            @Override
            public void run() {
                showProgressDialog("Loading...");
                CmdFactory cmdFactory = new EscFactory();
                Cmd cmd = cmdFactory.create();
                cmd.append(cmd.getHeaderCmd());
                CommonSetting commonSetting = new CommonSetting();
                commonSetting.setAlign(CommonEnum.ALIGN_MIDDLE);
                cmd.append(cmd.getCommonSettingCmd(commonSetting));
                BitmapSetting bitmapSetting = new BitmapSetting();
                BmpPrintMode mode = BmpPrintMode.MODE_MULTI_COLOR;
                bitmapSetting.setBmpPrintMode(mode);
                bitmapSetting.setBimtapLimitWidth(bmpPrintWidth* 8);
                try {
                    cmd.append(cmd.getBitmapCmd(bitmapSetting, mBitmap));
                } catch (SdkException e) {
                    e.printStackTrace();
                }
                cmd.append(cmd.getLFCRCmd());
                cmd.append(cmd.getCmdCutNew());
                if (rtPrinter != null)
                    rtPrinter.writeMsg(cmd.getAppendCmds());//Sync Write
                hideProgressDialog();
            }
        }).start();

    }

    private void pinPrint(){
        new Thread(new Runnable() {
            @Override
            public void run() {
                showProgressDialog(null);
                CmdFactory cmdFactory = new PinFactory();
                Cmd cmd = cmdFactory.create();
//                cmd.append(cmd.getHeaderCmd());
                int limitDots = Integer.parseInt(et_pic_width.getText().toString())*8;
                BitmapSetting bitmapSetting = new BitmapSetting();
                CommonSetting commonSetting = new CommonSetting();
                commonSetting.setAlign(CommonEnum.ALIGN_MIDDLE);//Left, Middle, Right
                cmd.append(cmd.getCommonSettingCmd(commonSetting));
                cmd.append(cmd.getLFCRCmd());
                cmd.append(cmd.getLFCRCmd());
                bitmapSetting.setBimtapLimitWidth(limitDots);
                try {
                    cmd.append(cmd.getBitmapCmd(bitmapSetting, mBitmap));
                } catch (SdkException e) {
                    e.printStackTrace();
                }
                cmd.append(cmd.getEndCmd());
                rtPrinter.writeMsg(cmd.getAppendCmds());
                hideProgressDialog();
            }
        }).start();


    }

    private void showPictureModeChooseDialog() {
        final BasicListDialog pictureModeChooseDialog = new BasicListDialog();
        ArrayList<String> contentList = new ArrayList<>();
//        contentList.add(getString(R.string.picture));
        contentList.add(getString(R.string.album));
        Bundle args = new Bundle();
        args.putString(BasicListDialog.BUNDLE_KEY_TITLE, getString(R.string.choose_picture_position));
        args.putStringArrayList(BasicListDialog.BUNDLE_KEY_CONTENT_LIST, contentList);
        pictureModeChooseDialog.setArguments(args);
        pictureModeChooseDialog.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                if (parent.getAdapter().getItem(position).toString().equals(getString(R.string.picture))) {
                    takeAPicture();
                } else {
                    openAlbum();
                }
                pictureModeChooseDialog.dismiss();
            }
        });
        pictureModeChooseDialog.show(getFragmentManager(), null);
    }

    private void takeAPicture() {
        if (!SaveMediaFileUtil.isExternalStorageWritable()) {
            ToastUtil.show(this, R.string.insert_sdcard_tip);
            return;
        }
        Intent intent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
        imageUri = SaveMediaFileUtil.getOutputMediaFileUri(getApplicationContext(), SaveMediaFileUtil.DIR_CAPTURE, SaveMediaFileUtil.MEDIA_TYPE_IMAGE);
        intent.putExtra(MediaStore.EXTRA_OUTPUT, imageUri);
        startActivityForResult(intent, CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE);
    }

    private void openAlbum() {
        Intent intent = new Intent(Intent.ACTION_PICK, android.provider.MediaStore.Images.Media.EXTERNAL_CONTENT_URI);
        startActivityForResult(intent, ALBUM_IMAGE_ACTIVITY_REQUEST_CODE);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        switch (requestCode) {
            case CAPTURE_IMAGE_ACTIVITY_REQUEST_CODE:
                if (resultCode == RESULT_OK) {
                    showImage(imageUri);
                }
                SimpleCursorTreeAdapter adapter;
                break;
            case ALBUM_IMAGE_ACTIVITY_REQUEST_CODE:
                if (resultCode == RESULT_OK) {
                    imageUri = data.getData();
                    showImage(imageUri);
                }
                break;
        }
    }

    private void showImage(Uri uri) {
        llUploadImage.setVisibility(View.GONE);
        flContent.setVisibility(View.VISIBLE);
        if (mBitmap != null) {
            mBitmap.recycle();
            mBitmap = null;
            System.gc();
        }
        try {
            mBitmap = MediaStore.Images.Media.getBitmap(getContentResolver(), uri);
            if(mBitmap==null)return;
        } catch (IOException e) {
            e.printStackTrace();
        }
        if (mBitmap.getWidth() > 210 * 8) {
            mBitmap = BitmapConvertUtil.decodeSampledBitmapFromUri(ImagePrintActivity.this, uri, 210 * 8, 4000);
        }else if (mBitmap.getWidth() > 99 * 8) {
            mBitmap = BitmapConvertUtil.decodeSampledBitmapFromUri(ImagePrintActivity.this, uri, 100 * 8, 4000);
        }else if (mBitmap.getWidth() > 72 * 8) {
            mBitmap = BitmapConvertUtil.decodeSampledBitmapFromUri(ImagePrintActivity.this, uri, 72 * 8, 4000);
        }else if (mBitmap.getWidth() > 48 * 8) {
            mBitmap = BitmapConvertUtil.decodeSampledBitmapFromUri(ImagePrintActivity.this, uri, 48 * 8, 4000);
        }
        Log.d(TAG, "mBitmap getWidth = " + mBitmap.getWidth());
        Log.d(TAG, "mBitmap getHeight = " + mBitmap.getHeight());
        ivImage.setImageBitmap(mBitmap);
    }
}
