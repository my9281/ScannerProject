package com.printer.example.activity;

import static com.rt.printerlibrary.utils.FuncUtils.getByteArr2Str;
import static com.rt.printerlibrary.utils.FuncUtils.subByteArr;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.AlertDialog;
import android.app.PendingIntent;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.hardware.usb.UsbConstants;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Message;
import android.support.annotation.IdRes;
import android.support.annotation.RequiresApi;
import android.support.v4.app.ActivityCompat;
import android.text.TextUtils;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ListView;
import android.widget.ProgressBar;
import android.widget.RadioGroup;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import com.printer.example.R;
import com.printer.example.app.BaseActivity;
import com.printer.example.app.BaseApplication;
import com.printer.example.dialog.BluetoothDeviceChooseDialog;
import com.printer.example.dialog.UsbDeviceChooseDialog;
import com.printer.example.utils.BaseEnum;
import com.printer.example.utils.LogUtils;
import com.printer.example.utils.SPUtils;
import com.printer.example.utils.TempletDemo;
import com.printer.example.utils.TimeRecordUtils;
import com.printer.example.utils.ToastUtil;
import com.printer.example.utils.TonyUtils;
import com.printer.example.view.FlowRadioGroup;
import com.rt.printerlibrary.bean.BluetoothEdrConfigBean;
import com.rt.printerlibrary.bean.PrinterStatusBean;
import com.rt.printerlibrary.bean.SerialPortConfigBean;
import com.rt.printerlibrary.bean.UsbConfigBean;
import com.rt.printerlibrary.bean.WiFiConfigBean;
import com.rt.printerlibrary.cmd.Cmd;
import com.rt.printerlibrary.cmd.CpclFactory;
import com.rt.printerlibrary.cmd.EscFactory;
import com.rt.printerlibrary.cmd.PinFactory;
import com.rt.printerlibrary.cmd.TscFactory;
import com.rt.printerlibrary.cmd.ZplFactory;
import com.rt.printerlibrary.connect.PrinterInterface;
import com.rt.printerlibrary.enumerate.CommonEnum;
import com.rt.printerlibrary.enumerate.ConnectStateEnum;
import com.rt.printerlibrary.exception.SdkException;
import com.rt.printerlibrary.factory.cmd.CmdFactory;
import com.rt.printerlibrary.factory.connect.BluetoothFactory;
import com.rt.printerlibrary.factory.connect.PIFactory;
import com.rt.printerlibrary.factory.connect.SerailPortFactory;
import com.rt.printerlibrary.factory.connect.UsbFactory;
import com.rt.printerlibrary.factory.connect.WiFiFactory;
import com.rt.printerlibrary.factory.printer.LabelPrinterFactory;
import com.rt.printerlibrary.factory.printer.PinPrinterFactory;
import com.rt.printerlibrary.factory.printer.PrinterFactory;
import com.rt.printerlibrary.factory.printer.ThermalPrinterFactory;
import com.rt.printerlibrary.factory.printer.UniversalPrinterFactory;
import com.rt.printerlibrary.ipscan.IpScanner;
import com.rt.printerlibrary.observer.PrinterObserver;
import com.rt.printerlibrary.observer.PrinterObserverManager;
import com.rt.printerlibrary.printer.RTPrinter;
import com.rt.printerlibrary.utils.ConnectListener;
import com.rt.printerlibrary.utils.FuncUtils;
import com.rt.printerlibrary.utils.PrintListener;
import com.rt.printerlibrary.utils.PrintStatusCmd;
import com.rt.printerlibrary.utils.PrinterStatusPareseUtils;
import com.rt.printerlibrary.utils.UpdateUtils;

import java.io.File;
import java.io.UnsupportedEncodingException;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;

import android_serialport_api01.SerialPortFinder;


public class MainActivity extends BaseActivity implements AdapterView.OnItemSelectedListener, View.OnClickListener,
        View.OnLongClickListener, PrinterObserver {

    private final static int SCAN_START = 1001;
    private final static int SCAN_FINISH = 1002;
    private final static int SCAN_ERROR = 1003;

    //权限申请
    private String[] NEED_PERMISSION;
    private List<String> NO_PERMISSION = new ArrayList<String>();
    private static final int REQUEST_CAMERA = 0;
    private final int REQUEST_ENABLE_BT = 101;
    private TextView tv_ver;
    private RadioGroup rg_cmdtype;
    private FlowRadioGroup rg_connect;
    private Button btn_selftest_print, btn_txt_print, btn_img_print, btn_template_print, btn_barcode_print,
            btn_beep, btn_all_cut, btn_cash_box, btn_wifi_setting, btn_wifi_ipdhcp, btn_cmd_test, btn_label_setting,
            btn_print_status, btn_print_tsc_density, btn_setXYPoint, btn_read_optocoupler, btn_learnNoPaper,
            btn_FactoryDataReset, btn_LearnLabel,btn_cash_box_status;
    private Button btn_disConnect, btn_connect;
    private TextView tv_device_selected;
    private Button btn_connected_list;
    private ProgressBar pb_connect;
    private Button btn_test,btn_print_update,btn_print_firmware_version;
    private Spinner mSpComPath, mSpBps;


    @BaseEnum.ConnectType
    private int checkedConType = BaseEnum.CON_USB;
    private RTPrinter rtPrinter = null;
    private PrinterFactory printerFactory;
    private final String SP_KEY_IP = "ip";
    private final String SP_KEY_PORT = "port";
    private Object configObj;
    private ArrayList<PrinterInterface> printerInterfaceArrayList = new ArrayList<>();
    private PrinterInterface curPrinterInterface = null;
    private BroadcastReceiver broadcastReceiver;//USB Attach-Deattached Receiver
    private BluetoothAdapter mBluetoothAdapter;
    private int iprintTimes = 0;
    private List<UsbDevice> mList;
    public UsbConfigBean usbconfigObj;
    private SerialPortConfigBean mSerialPortConfigBean = new SerialPortConfigBean();
    private String[] mDevices;
    private String[] mBaudrates;
    private int mDeviceIndex;
    private int mBaudrateIndex;
    private List<IpScanner.DeviceBean> deviceBeanList;
    private PrintStatusCmd printStatusCmd = PrintStatusCmd.cmd_print_label;


    private void CheckAllPermission() {
        NO_PERMISSION.clear();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            NEED_PERMISSION = new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE,
                    Manifest.permission.BLUETOOTH_CONNECT,
                    Manifest.permission.BLUETOOTH_SCAN,
                    Manifest.permission.BLUETOOTH_ADVERTISE,
                    Manifest.permission.ACCESS_FINE_LOCATION
            };
        } else {
            NEED_PERMISSION = new String[]{Manifest.permission.WRITE_EXTERNAL_STORAGE,
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
            };
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            for (int i = 0; i < NEED_PERMISSION.length; i++) {
                if (checkSelfPermission(NEED_PERMISSION[i]) != PackageManager.PERMISSION_GRANTED) {
                    NO_PERMISSION.add(NEED_PERMISSION[i]);
                }
            }
            if (NO_PERMISSION.size() == 0) {
                recordVideo();
            } else {
                requestPermissions(NO_PERMISSION.toArray(new String[NO_PERMISSION.size()]), REQUEST_CAMERA);
            }
        } else {
            recordVideo();
        }

    }

    @SuppressLint("HandlerLeak")
    private Handler handler = new Handler() {
        @RequiresApi(api = Build.VERSION_CODES.LOLLIPOP)
        @Override
        public void handleMessage(Message msg) {
            switch (msg.what) {
                case 11:
                    connectBluetoothByMac();
                    break;
                case SCAN_START:
                    Log.d("rongta", "SCAN_START-------------------");
                    break;
                case SCAN_FINISH:
                    deviceBeanList = (List<IpScanner.DeviceBean>) msg.obj;
                    Log.d("rongta", "SCAN_FINISH-------------------");
//                    if(deviceBeanList != null){
//                        for(IpScanner.DeviceBean d:deviceBeanList){
//                            Log.d("rongta","ip = "+d.getDeviceIp());
//                            Log.d("rongta","mac = "+d.getMacAddress());
//                            Log.d("rongta","dhcp = "+d.isDHCPEnable());
//                        }
//                    }
                    pb_connect.setVisibility(View.GONE);
                    showIpDialog(getString(R.string.tip_select_ip_device));
                    break;
                case SCAN_ERROR:
                    pb_connect.setVisibility(View.GONE);
                    showIpDialog(getString(R.string.tip_select_ip_device));
                    break;
            }
        }
    };


    private void recordVideo() {
        Log.d("MainActivity", "有权限了");
    }

    @Override
    protected void onResume() {
        super.onResume();
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        CheckAllPermission();
        setContentView(R.layout.activity_main);
        initView();
        addListener();
        init();
        btnVisible();
    }

    private void btnVisible() {
        int currentCmdType = BaseApplication.getInstance().getCurrentCmdType();
        btn_setXYPoint.setVisibility(currentCmdType == BaseEnum.CMD_TSC?View.VISIBLE:View.GONE);
        btn_print_status.setVisibility(View.VISIBLE);
        btn_barcode_print.setVisibility(currentCmdType == BaseEnum.CMD_PIN?View.GONE:View.VISIBLE);
        btn_cash_box_status.setVisibility(currentCmdType == BaseEnum.CMD_ESC?View.VISIBLE:View.GONE);
    }


    @RequiresApi(api = Build.VERSION_CODES.LOLLIPOP)
    private void showIpDialog(String title) {
        boolean findDevice = false;
        final AlertDialog.Builder builder = new AlertDialog.Builder(this);
        String[] data = new String[]{
                getResources().getString(R.string.tip_can_find_net_device)
        };
        if (deviceBeanList != null && deviceBeanList.size() != 0) {
            findDevice = true;
            data = new String[deviceBeanList.size()];
            for (int i = 0; i < deviceBeanList.size(); i++) {
                data[i] = "Ip :" + deviceBeanList.get(i).getDeviceIp() + ":" + deviceBeanList.get(i).getDevicePort() + "\n";
                data[i] += "Mac :" + deviceBeanList.get(i).getMacAddress();
            }
        }
        View view = LayoutInflater.from(this).inflate(R.layout.dialog_list_basic, null);
        TextView textView = view.findViewById(R.id.tv_dialog_list_basic_title);
        textView.setText(title);
        ListView listView = view.findViewById(R.id.lv_dialog_list_basic_content);
        ArrayAdapter<String> adapter = new ArrayAdapter<String>(this, R.layout.listdialog_item_simple, R.id.tv_list_item);
        for (String e : data)
            adapter.add(e);
        listView.setAdapter(adapter);
        builder.setView(view);
        builder.setNegativeButton(getResources().getString(R.string.dialog_cancel), null);
        final AlertDialog dialog = builder.show();
        dialog.setCanceledOnTouchOutside(false);
        final boolean finalFindDevice = findDevice;
        listView.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> adapterView, View view, int i, long l) {
                if (finalFindDevice) {
                    String ip = deviceBeanList.get(i).getDeviceIp();
                    int port = deviceBeanList.get(i).getDevicePort();
                    configObj = new WiFiConfigBean(ip, port);
                    tv_device_selected.setText(configObj.toString());
                    tv_device_selected.setTag(BaseEnum.HAS_DEVICE);
                    isConfigPrintEnable(configObj);
                } else {
                    tv_device_selected.setText(getString(R.string.tip_ip_device));
                }

                dialog.dismiss();
            }
        });
        dialog.getButton(dialog.BUTTON_NEGATIVE).setBackground(getDrawable(R.drawable.text_border_selector));
    }


    public void initView() {
        tv_ver = findViewById(R.id.tv_ver);
        rg_cmdtype = findViewById(R.id.rg_cmdtype);
        rg_connect = findViewById(R.id.rg_connect);
        btn_selftest_print = findViewById(R.id.btn_selftest_print);
        btn_txt_print = findViewById(R.id.btn_txt_print);
        btn_img_print = findViewById(R.id.btn_img_print);
        btn_connect = findViewById(R.id.btn_connect);
        btn_disConnect = findViewById(R.id.btn_disConnect);
        tv_device_selected = findViewById(R.id.tv_device_selected);
        btn_template_print = findViewById(R.id.btn_template_print);
        btn_barcode_print = findViewById(R.id.btn_barcode_print);
        btn_connected_list = findViewById(R.id.btn_connected_list);
        btn_cash_box = findViewById(R.id.btn_cash_box);
        btn_all_cut = findViewById(R.id.btn_all_cut);
        btn_beep = findViewById(R.id.btn_beep);
        btn_wifi_setting = findViewById(R.id.btn_wifi_setting);
        btn_wifi_ipdhcp = findViewById(R.id.btn_wifi_ipdhcp);
        btn_cmd_test = findViewById(R.id.btn_cmd_test);
        pb_connect = findViewById(R.id.pb_connect);
        btn_test = findViewById(R.id.btn_test);
        btn_label_setting = findViewById(R.id.btn_label_setting);
        btn_print_status = findViewById(R.id.btn_print_status);
        btn_print_tsc_density = findViewById(R.id.btn_print_tsc_density);
        btn_setXYPoint = findViewById(R.id.btn_setXYPoint);
        btn_read_optocoupler = findViewById(R.id.btn_read_optocoupler);
        btn_learnNoPaper = findViewById(R.id.btn_learnNoPaper);
        btn_FactoryDataReset = findViewById(R.id.btn_FactoryDataReset);
        btn_LearnLabel = findViewById(R.id.btn_LearnLabel);
        btn_cash_box_status = findViewById(R.id.btn_cash_box_status);
        btn_print_update = findViewById(R.id.btn_print_update);
        btn_print_firmware_version = findViewById(R.id.btn_print_firmware_version);
    }

    public void init() {
        BaseApplication.instance.setCurrentCmdType(BaseEnum.CMD_ESC);
        printerFactory = new UniversalPrinterFactory();
        rtPrinter = printerFactory.create();
        tv_ver.setText("PrinterExample Ver: v" + TonyUtils.getVersionName(this));
        PrinterObserverManager.getInstance().add(this);//添加连接状态监听
        rg_cmdtype.check(R.id.rb_cmd_esc);
        rg_connect.check(R.id.rb_connect_bluetooth);
    }

    public void addListener() {
        btn_selftest_print.setOnClickListener(this);
        btn_txt_print.setOnClickListener(this);
        btn_img_print.setOnClickListener(this);
        btn_connect.setOnClickListener(this);
        btn_disConnect.setOnClickListener(this);
        btn_template_print.setOnClickListener(this);
        tv_device_selected.setOnClickListener(this);
        tv_device_selected.setOnLongClickListener(this);
        btn_barcode_print.setOnClickListener(this);
        btn_connected_list.setOnClickListener(this);
        btn_cash_box.setOnClickListener(this);
        btn_all_cut.setOnClickListener(this);
        btn_beep.setOnClickListener(this);
        btn_wifi_setting.setOnClickListener(this);
        btn_wifi_ipdhcp.setOnClickListener(this);
        btn_cmd_test.setOnClickListener(this);
        btn_test.setOnClickListener(this);
        btn_label_setting.setOnClickListener(this);
        btn_print_status.setOnClickListener(this);
        btn_print_tsc_density.setOnClickListener(this);
        btn_setXYPoint.setOnClickListener(this);
        btn_read_optocoupler.setOnClickListener(this);
        btn_learnNoPaper.setOnClickListener(this);
        btn_FactoryDataReset.setOnClickListener(this);
        btn_LearnLabel.setOnClickListener(this);
        btn_cash_box_status.setOnClickListener(this);
        btn_print_update.setOnClickListener(this);
        btn_print_firmware_version.setOnClickListener(this);
        radioButtonCheckListener();//single button listener
    }

    private void radioButtonCheckListener() {
        rg_cmdtype.setOnCheckedChangeListener(new RadioGroup.OnCheckedChangeListener() {
            @Override
            public void onCheckedChanged(RadioGroup radioGroup, @IdRes int i) {
                switch (i) {
                    case R.id.rb_cmd_pin://针打
                        BaseApplication.instance.setCurrentCmdType(BaseEnum.CMD_PIN);
                        printerFactory = new PinPrinterFactory();
                        rtPrinter = printerFactory.create();
                        rtPrinter.setPrinterInterface(curPrinterInterface);
                        btn_barcode_print.setVisibility(View.GONE);
                        btn_label_setting.setVisibility(View.GONE);
                        break;
                    case R.id.rb_cmd_esc://esc
                        BaseApplication.instance.setCurrentCmdType(BaseEnum.CMD_ESC);
                        printerFactory = new ThermalPrinterFactory();
                        rtPrinter = printerFactory.create();
                        rtPrinter.setPrinterInterface(curPrinterInterface);
                        btn_barcode_print.setVisibility(View.VISIBLE);
                        btn_label_setting.setVisibility(View.GONE);
                        printStatusCmd = PrintStatusCmd.cmd_print_100402;
                        break;
                    case R.id.rb_cmd_tsc://tsc
                        BaseApplication.instance.setCurrentCmdType(BaseEnum.CMD_TSC);
                        printerFactory = new LabelPrinterFactory();
                        rtPrinter = printerFactory.create();
                        rtPrinter.setPrinterInterface(curPrinterInterface);
                        btn_barcode_print.setVisibility(View.VISIBLE);
//                        btn_label_setting.setVisibility(View.VISIBLE);
                        printStatusCmd = PrintStatusCmd.cmd_Normal;
                        break;
                    case R.id.rb_cmd_cpcl://cpcl
                        BaseApplication.instance.setCurrentCmdType(BaseEnum.CMD_CPCL);
                        printerFactory = new LabelPrinterFactory();
                        rtPrinter = printerFactory.create();
                        rtPrinter.setPrinterInterface(curPrinterInterface);
                        btn_barcode_print.setVisibility(View.VISIBLE);
//                        btn_label_setting.setVisibility(View.VISIBLE);
                        printStatusCmd = PrintStatusCmd.cmd_Normal;
                        break;
                    case R.id.rb_cmd_zpl://zpl
                        BaseApplication.instance.setCurrentCmdType(BaseEnum.CMD_ZPL);
                        printerFactory = new LabelPrinterFactory();
                        rtPrinter = printerFactory.create();
                        rtPrinter.setPrinterInterface(curPrinterInterface);
                        btn_barcode_print.setVisibility(View.VISIBLE);
                        btn_label_setting.setVisibility(View.GONE);
                        printStatusCmd = PrintStatusCmd.cmd_Normal;
                        break;
                }
                BaseApplication.getInstance().setRtPrinter(rtPrinter);
            }
        });

        rg_connect.setOnCheckedChangeListener(new RadioGroup.OnCheckedChangeListener() {
            @Override
            public void onCheckedChanged(RadioGroup radioGroup, @IdRes int i) {
                doDisConnect();//有切换的话就断开
                switch (i) {
                    case R.id.rb_connect_wifi://WiFi
                        checkedConType = BaseEnum.CON_WIFI;
                        tv_device_selected.setText(R.string.tip_ip_device);
                        break;
                    case R.id.rb_connect_bluetooth://bluetooth
                        checkedConType = BaseEnum.CON_BLUETOOTH;
                        tv_device_selected.setText(R.string.please_connect);
                        break;
//                    case R.id.rb_connect_bluetooth_ble://bluetooth_ble
////                        checkedConType = BaseEnum.CON_BLUETOOTH_BLE;
//                        break;
                    case R.id.rb_connect_usb://usb
                        checkedConType = BaseEnum.CON_USB;
                        tv_device_selected.setText(R.string.please_connect);
                        break;
                    case R.id.rb_connect_com:
                        checkedConType = BaseEnum.CON_COM;
                        tv_device_selected.setText(R.string.please_connect);
                        break;
                }
                BaseApplication.getInstance().setCurrentConnectType(checkedConType);
            }
        });
    }

    @Override
    public void onClick(View view) {
        switch (view.getId()) {
            case R.id.btn_selftest_print:
                selfTestPrint();
                break;
            case R.id.btn_txt_print:
                textPrint();
                break;
            case R.id.btn_img_print:
                imagePrint();
                break;
            case R.id.btn_disConnect:
                doDisConnect();
                break;
            case R.id.btn_connect:
                doConnect();
                break;
            case R.id.btn_template_print:
                toTemplateActivity();
                break;
            case R.id.tv_device_selected:
                showConnectDialog();
                break;
            case R.id.btn_barcode_print://条码打印
                turn2Activity(BarcodeActivity.class);
                break;
            case R.id.btn_connected_list://显示多连接
                showConnectedListDialog();
                break;
            case R.id.btn_beep://蜂鸣测试
                beepTest();
                break;
            case R.id.btn_all_cut://切刀测试-全切
                allCutTest();
                break;
            case R.id.btn_cash_box://钱箱测试
                cashboxTest();
                break;
            case R.id.btn_wifi_setting://WiFi设置
                turn2Activity(WifiSettingActivity.class);
                break;
            case R.id.btn_wifi_ipdhcp://IP/DHCP设置
                turn2Activity(WifiIpDhcpSettingActivity.class);
                break;
            case R.id.btn_cmd_test:
                turn2Activity(CmdTestActivity.class);
                break;
            case R.id.btn_test:
                testTsc();
                break;
            case R.id.btn_label_setting:
                toLabelSettingActivity();
                break;
            case R.id.btn_print_status:
                getPrintStauts();
                break;
            case R.id.btn_cash_box_status:
                getCashBoxStatus();
                break;
            case R.id.btn_print_firmware_version:
                getFirmwareVersion();
                break;
            case R.id.btn_print_update://Firmware update
                turn2Activity(FirmwareUpdateActivity.class);
            default:
                break;
        }
    }

    /**
     * 1 print status
     * 2 cash box
     * 3 Firmware Version
     */
    private int msgType=1;
    /**
     * Firmware Version
     */
    private void getFirmwareVersion() {
        msgType=3;
        if (rtPrinter == null) return;
        if(BaseApplication.getInstance().getCurrentCmdType()!=BaseEnum.CMD_ESC){
            rtPrinter.writeMsgAsync(new TscFactory().create().getLabelFirmwareversion());
        }
    }

    /**
     * get CashBox status
     */
    private void getCashBoxStatus() {
        msgType=2;
        if (rtPrinter == null) return;
        CmdFactory cmdFactory = new EscFactory();
        Cmd cmd = cmdFactory.create();
        rtPrinter.writeMsgAsync(cmd.getCashdrawerStatus());
    }

    /**
     * get status
     *
     *
     */
    private void getPrintStauts() {
        msgType=1;
        if (rtPrinter == null) return;
        CmdFactory cmdFactory = new EscFactory();
        Cmd cmd = cmdFactory.create();
        rtPrinter.writeMsgAsync(cmd.getPrintStausCmd(printStatusCmd));
    }

    /**
     * setPrinterStatusListener
     */
    /*private void setPrinterStatusListener() {
        if (rtPrinter == null) return;
        rtPrinter.setPrintListener(new PrintListener() {
            @Override
            public void onPrinterStatus(PrinterStatusBean StatusBean) {
                //Accept printer status here
                String msg = PrinterStatusPareseUtils.getPrinterStatusStr(StatusBean);
                Log.i("rongtaPrint", "onPrinterStatus: " + msg);
                if (!msg.isEmpty())
                    ToastUtil.show(MainActivity.this, "print status：" + msg);
            }
        });
    }*/

    /**
     * turn to Label Setting[CPCL]
     */
    private void toLabelSettingActivity() {
        turn2Activity(LabelSettingActivity.class);
    }

    private void testTsc() {
        if (rtPrinter == null) return;
        TonyUtils.Tsc_InitLabelPrint(rtPrinter);
        String strPrintTxt = TonyUtils.printText("80", "80", "TSS24.BF2", "0", "1", "1", "Hello,容大!");
        String strPrint = TonyUtils.setPRINT("1", "1");
        try {
            rtPrinter.writeMsgAsync(strPrintTxt.getBytes("GBK"));
        } catch (UnsupportedEncodingException e) {
            e.printStackTrace();
        }
        rtPrinter.writeMsgAsync(strPrint.getBytes());
    }

    private void initBroadcast() {
        broadcastReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                // TODO Auto-generated method stub
                String action = intent.getAction();
                if (UsbManager.ACTION_USB_DEVICE_DETACHED.equals(action)) {
                    //   ToastUtil.show(context,"接收到断开信息");
                    if (BaseApplication.getInstance().getCurrentConnectType() == BaseEnum.CON_USB) {
                        doDisConnect();//断开USB连接， Disconnect USB connection.
                    }
                }
                if (UsbManager.ACTION_USB_DEVICE_ATTACHED.equals(action)) {
                    //    ToastUtil.show(context,"插入USB");
                }
            }
        };
        IntentFilter intentFilter = new IntentFilter();
        intentFilter.addAction(UsbManager.ACTION_USB_DEVICE_ATTACHED);
        intentFilter.addAction(UsbManager.ACTION_USB_ACCESSORY_ATTACHED);
        intentFilter.addAction(UsbManager.ACTION_USB_DEVICE_DETACHED);
        registerReceiver(broadcastReceiver, intentFilter);
    }

    /**
     * 蜂鸣测试
     * beep
     */
    private void beepTest() {
        if (rtPrinter != null) {
            CmdFactory cmdFactory = new EscFactory();
            Cmd cmd = cmdFactory.create();
            cmd.append(cmd.getBeepCmd());
            rtPrinter.writeMsgAsync(cmd.getAppendCmds());
        }
    }

    /**
     * 全切测试
     * cut
     */
    private void allCutTest() {
        if (rtPrinter != null) {
            CmdFactory cmdFactory = new EscFactory();
            Cmd cmd = cmdFactory.create();
            cmd.append(cmd.getAllCutCmd());
            rtPrinter.writeMsgAsync(cmd.getAppendCmds());
        }
    }

    /**
     * 钱箱测试
     * cash
     */
    private void cashboxTest() {
        if (rtPrinter != null) {
            CmdFactory cmdFactory = new EscFactory();
            Cmd cmd = cmdFactory.create();
            cmd.append(cmd.getOpenMoneyBoxCmd());//Open cashbox use default setting[0x00,0x20,0x01]
            rtPrinter.writeMsgAsync(cmd.getAppendCmds());
        }
    }

    /**
     * 显示已连接设备窗口
     */
    private void showConnectedListDialog() {
        AlertDialog.Builder dialog = new AlertDialog.Builder(this);
        dialog.setTitle(R.string.dialog_title_connected_devlist);
        String[] devList = new String[printerInterfaceArrayList.size()];
        for (int i = 0; i < devList.length; i++) {
            devList[i] = printerInterfaceArrayList.get(i).getConfigObject().toString();
        }
        if (devList.length > 0) {
            dialog.setItems(devList, new DialogInterface.OnClickListener() {
                @Override
                public void onClick(DialogInterface dialogInterface, int i) {
                    PrinterInterface printerInter = printerInterfaceArrayList.get(i);
                    tv_device_selected.setText(printerInter.getConfigObject().toString());
                    rtPrinter.setPrinterInterface(printerInter);//设置连接方式 Connection port settings
                    tv_device_selected.setTag(BaseEnum.HAS_DEVICE);
                    curPrinterInterface = printerInter;
                    //  BaseApplication.getInstance().setRtPrinter(rtPrinter);//设置全局RTPrinter
                    if (printerInter.getConnectState() == ConnectStateEnum.Connected) {
                        setPrintEnable(true);
                    } else {
                        setPrintEnable(false);
                    }
                }
            });
        } else {
            dialog.setMessage(R.string.pls_connect_printer_first);
        }
        dialog.setNegativeButton(R.string.dialog_cancel, null);
        dialog.show();
    }

    private void doConnect() {
        if (checkedConType != BaseEnum.CON_USB) {
            if (Integer.parseInt(tv_device_selected.getTag().toString()) == BaseEnum.NO_DEVICE) {//选择设备
                showAlertDialog(getString(R.string.main_pls_choose_device));
                return;
            }
        }
        pb_connect.setVisibility(View.VISIBLE);
        switch (checkedConType) {
            case BaseEnum.CON_WIFI:
                WiFiConfigBean wiFiConfigBean = (WiFiConfigBean) configObj;
                connectWifi(wiFiConfigBean);
                break;
            case BaseEnum.CON_BLUETOOTH:
                TimeRecordUtils.record("RT连接start：", System.currentTimeMillis());
                BluetoothEdrConfigBean bluetoothEdrConfigBean = (BluetoothEdrConfigBean) configObj;
                iprintTimes = 0;
                connectBluetooth(bluetoothEdrConfigBean);
//              connectBluetoothByMac();
                break;
            case BaseEnum.CON_USB:
                if (Integer.parseInt(tv_device_selected.getTag().toString()) == BaseEnum.NO_DEVICE) {
                    showUSBDeviceChooseDialog();
                } else {
                    UsbConfigBean usbConfigBean = (UsbConfigBean) configObj;
                    connectUSB(usbConfigBean);
                }
                break;
            case BaseEnum.CON_COM:
                connectSerialPort(mSerialPortConfigBean);
                break;
            default:
                pb_connect.setVisibility(View.GONE);
                break;
        }

    }


    private int getUsbCount() {
        mList = new ArrayList<>();
        UsbManager mUsbManager = (UsbManager) getApplication().getSystemService(Context.USB_SERVICE);
        HashMap<String, UsbDevice> deviceList = mUsbManager.getDeviceList();
        LogUtils.d(TAG, "deviceList size = " + deviceList.size());
        Iterator<UsbDevice> deviceIterator = deviceList.values().iterator();

        while (deviceIterator.hasNext()) {
            UsbDevice device = deviceIterator.next();
            LogUtils.d(TAG, "device getDeviceName" + device.getDeviceName());
            LogUtils.d(TAG, "device getVendorId" + device.getVendorId());
            LogUtils.d(TAG, "device getProductId" + device.getProductId());
            if (isPrinterDevice(device))
                mList.add(device);
        }
        return mList.size();

    }

    private static boolean isPrinterDevice(UsbDevice device) {
        if (device == null) {
            return false;
        }
        if (device.getInterfaceCount() == 0) {
            return false;
        }
        for (int i = 0; i < device.getInterfaceCount(); i++) {
            android.hardware.usb.UsbInterface usbInterface = device.getInterface(i);
            if (usbInterface.getInterfaceClass() == UsbConstants.USB_CLASS_PRINTER) {
                return true;
            }
        }
        return false;
    }


    private void connectBluetoothByMac() {
        BluetoothDevice device = BluetoothAdapter.getDefaultAdapter().getRemoteDevice("DC:0D:30:00:1F:79");//直接连接蓝牙mac 地址 00:00:0E:01:D0:32
        BluetoothEdrConfigBean bluetoothEdrConfigBean = new BluetoothEdrConfigBean(device);
        PIFactory piFactory = new BluetoothFactory();
        PrinterInterface printerInterface = piFactory.create();
        printerInterface.setConfigObject(bluetoothEdrConfigBean);
        rtPrinter.setPrinterInterface(printerInterface);
        rtPrinter.setConnectListener(new ConnectListener() { //必须在connect之后，设置监听
            @Override
            public void onPrinterConnected(Object configObj) {
                try {
                    TempletDemo.getInstance(rtPrinter, MainActivity.this).escTemplet();
                } catch (UnsupportedEncodingException e) {
                    e.printStackTrace();
                } catch (SdkException e) {
                    e.printStackTrace();
                }
                iprintTimes++;
            }
            @Override
            public void onPrinterDisconnect(Object configObj) {
                if (iprintTimes < 5) {
                    new Thread(new Runnable() {
                        @Override
                        public void run() {
                            Message message = new Message();
                            message.what = 11;
                            handler.sendMessage(message);
                        }
                    }).start();

                }
            }
            @Override
            public void onPrinterWritecompletion(Object configObj) { //写入完成时,断开,要改为收到打印完成状态
                try {
                    Thread.sleep(500);//根据打印内容的长短，来设置 Set according to the length of the printed content
                } catch (InterruptedException e) {
                    e.printStackTrace();
                }
                rtPrinter.disConnect();
            }
        });
        try {
            rtPrinter.connect(bluetoothEdrConfigBean);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void connectSerialPort(SerialPortConfigBean serialPortConfigBean) {
        PIFactory piFactory = new SerailPortFactory();
        PrinterInterface printerInterface = piFactory.create();
        printerInterface.setConfigObject(serialPortConfigBean);
        rtPrinter.setPrinterInterface(printerInterface);
        try {
            rtPrinter.connect(serialPortConfigBean);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }


    private void connectBluetooth(BluetoothEdrConfigBean bluetoothEdrConfigBean) {
        PIFactory piFactory = new BluetoothFactory();
        PrinterInterface printerInterface = piFactory.create();
        printerInterface.setConfigObject(bluetoothEdrConfigBean);
        rtPrinter.setPrinterInterface(printerInterface);
        try {
            rtPrinter.connect(bluetoothEdrConfigBean);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }


    private void connectWifi(WiFiConfigBean wiFiConfigBean) {
        PIFactory piFactory = new WiFiFactory();
        PrinterInterface printerInterface = piFactory.create();
        printerInterface.setConfigObject(wiFiConfigBean);
        rtPrinter.setPrinterInterface(printerInterface);
        try {
            rtPrinter.connect(wiFiConfigBean);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void connectUSB(UsbConfigBean usbConfigBean) {
        UsbManager mUsbManager = (UsbManager) getSystemService(Context.USB_SERVICE);
        PIFactory piFactory = new UsbFactory();
        PrinterInterface printerInterface = piFactory.create();
        printerInterface.setConfigObject(usbConfigBean);
        rtPrinter.setPrinterInterface(printerInterface);

        if (mUsbManager.hasPermission(usbConfigBean.usbDevice)) {
            try {
                rtPrinter.connect(usbConfigBean);
            } catch (Exception e) {
                e.printStackTrace();
            }
        } else {
            mUsbManager.requestPermission(usbConfigBean.usbDevice, usbConfigBean.pendingIntent);
        }

    }

    private void toTemplateActivity() {
        turn2Activity(TempletPrintActivity.class);
    }


    private void doDisConnect() {

        if (Integer.parseInt(tv_device_selected.getTag().toString()) == BaseEnum.NO_DEVICE) {//未选择设备
//            showAlertDialog(getString(R.string.main_discon_click_repeatedly));
            return;
        }

        if (rtPrinter != null && rtPrinter.getPrinterInterface() != null) {
            try {
                rtPrinter.disConnect();
            } catch (Exception e) {

            }
        }
        tv_device_selected.setText(getString(R.string.please_connect));
        tv_device_selected.setTag(BaseEnum.NO_DEVICE);
        setPrintEnable(false);
    }

    private void showConnectDialog() {
        //doDisConnect();
        switch (checkedConType) {
            case BaseEnum.CON_WIFI:
                //showWifiChooseDialog();
                if (pb_connect.getVisibility() == View.VISIBLE) {
                    return;
                } else {
                    pb_connect.setVisibility(View.VISIBLE);
                }

                new IpScanner() {
                    @Override
                    public void onSearchStart() {
                        Message message = new Message();
                        message.what = SCAN_START;
                        handler.sendMessage(message);
                    }

                    @Override
                    public void onSearchFinish(List devicesList) {
                        Message message = new Message();
                        message.obj = devicesList;
                        message.what = SCAN_FINISH;
                        handler.sendMessage(message);
                    }

                    @Override
                    public void onSearchError(String msg) {
                        Message message = new Message();
                        message.what = SCAN_ERROR;
                        message.obj = msg;
                        handler.sendMessage(message);
                    }
                }.start();
                break;
            case BaseEnum.CON_BLUETOOTH:
                if (!BluetoothAdapter.getDefaultAdapter().isEnabled()) { //蓝牙未开启，则开启蓝牙
                    Intent enableIntent = new Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE);
                    if (ActivityCompat.checkSelfPermission(this, Manifest.permission.BLUETOOTH_CONNECT) != PackageManager.PERMISSION_GRANTED) {
                        // TODO: Consider calling
                        //    ActivityCompat#requestPermissions
                        // here to request the missing permissions, and then overriding
                        //   public void onRequestPermissionsResult(int requestCode, String[] permissions,
                        //                                          int[] grantResults)
                        // to handle the case where the user grants the permission. See the documentation
                        // for ActivityCompat#requestPermissions for more details.
                        return;
                    }
                    startActivityForResult(enableIntent, REQUEST_ENABLE_BT);
                } else {
                showBluetoothDeviceChooseDialog();
                }
                break;
            case BaseEnum.CON_USB:
                showUSBDeviceChooseDialog();
                break;
            case BaseEnum.CON_COM:
                showCOMDeviceChooseDialog();
                break;
            default:
                break;
        }
    }

    private void selfTestPrint() {
        switch (BaseApplication.getInstance().getCurrentCmdType()) {
            case BaseEnum.CMD_PIN:
                pinSelftestPrint();
                break;
            case BaseEnum.CMD_ESC:
                escSelftestPrint();
                break;
            case BaseEnum.CMD_TSC:
                tscSelftestPrint();
                break;
            case BaseEnum.CMD_CPCL:
                cpclSelftestPrint();
                break;
            case BaseEnum.CMD_ZPL:
                zplSelftestPrint();
                break;
            default:
                break;
        }
    }

    private void cpclSelftestPrint() {
        CmdFactory cmdFactory = new CpclFactory();
        Cmd cmd = cmdFactory.create();
        cmd.append(cmd.getSelfTestCmd());
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }

    private void zplSelftestPrint() {
        CmdFactory cmdFactory = new ZplFactory();
        Cmd cmd = cmdFactory.create();
        cmd.append(cmd.getHeaderCmd());
        cmd.append(cmd.getSelfTestCmd());
        cmd.append(cmd.getEndCmd());
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }

    private void tscSelftestPrint() {
        CmdFactory cmdFactory = new TscFactory();
        Cmd cmd = cmdFactory.create();
        cmd.append(cmd.getHeaderCmd());
        cmd.append(cmd.getLFCRCmd());
        cmd.append(cmd.getLFCRCmd());
        cmd.append(cmd.getSelfTestCmd());
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }

    private void escSelftestPrint() {
        CmdFactory cmdFactory = new EscFactory();
        Cmd cmd = cmdFactory.create();
        cmd.append(cmd.getHeaderCmd());
        cmd.append(cmd.getLFCRCmd());
        cmd.append(cmd.getSelfTestCmd());
        cmd.append(cmd.getLFCRCmd());
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }

    private void pinSelftestPrint() {
        CmdFactory cmdFactory = new PinFactory();
        Cmd cmd = cmdFactory.create();
        cmd.append(cmd.getHeaderCmd());
        cmd.append(cmd.getLFCRCmd());
        cmd.append(cmd.getLFCRCmd());
        cmd.append(cmd.getSelfTestCmd());
        rtPrinter.writeMsgAsync(cmd.getAppendCmds());
    }

    private void imagePrint() {
        turn2Activity(ImagePrintActivity.class);
    }

    private void textPrint() {
        switch (BaseApplication.getInstance().getCurrentCmdType()) {
            case BaseEnum.CMD_ESC:
                turn2Activity(TextPrintESCActivity.class);
                break;
            default:
                turn2Activity(TextPrintActivity.class);
                break;
        }
    }

    @Override
    public void printerObserverCallback(final PrinterInterface printerInterface, final int state) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                pb_connect.setVisibility(View.GONE);
                switch (state) {
                    case CommonEnum.CONNECT_STATE_SUCCESS:
                        TimeRecordUtils.record("RT连接end：", System.currentTimeMillis());
                        showToast(printerInterface.getConfigObject().toString() + getString(R.string._main_connected));
                        tv_device_selected.setText(printerInterface.getConfigObject().toString());
                        tv_device_selected.setTag(BaseEnum.HAS_DEVICE);
                        curPrinterInterface = printerInterface;//设置为当前连接， set current Printer Interface
                        printerInterfaceArrayList.add(printerInterface);//多连接-添加到已连接列表
                        rtPrinter.setPrinterInterface(printerInterface);
                        BaseApplication.getInstance().setRtPrinter(rtPrinter);
                        setPrintEnable(true);
//                        setPrinterStatusListener();//StatusListener()
                        break;
                    case CommonEnum.CONNECT_STATE_INTERRUPTED:
                        if (printerInterface != null && printerInterface.getConfigObject() != null) {
                            showToast(printerInterface.getConfigObject().toString() + getString(R.string._main_disconnect));
                        } else {
                            showToast(getString(R.string._main_disconnect));
                        }
                        TimeRecordUtils.record("RT连接断开：", System.currentTimeMillis());
                        tv_device_selected.setText(R.string.please_connect);
                        tv_device_selected.setTag(BaseEnum.NO_DEVICE);
                        curPrinterInterface = null;
                        printerInterfaceArrayList.remove(printerInterface);//多连接-从已连接列表中移除
                        BaseApplication.getInstance().setRtPrinter(null);
                        setPrintEnable(false);
                        break;
                    default:
                        break;
                }
            }
        });
    }

    @Override
    public void printerReadMsgCallback(PrinterInterface printerInterface, final byte[] bytes) {
        Log.i(TAG, "printerReadMsgCallback: "+FuncUtils.ByteArrToHex(bytes));
        switch (msgType){
            case 1://Accept printer status here
                PrinterStatusBean StatusBean = PrinterStatusPareseUtils.parsePrinterStatusResult(bytes);
                String msg = PrinterStatusPareseUtils.getPrinterStatusStr(StatusBean);
                Log.i("rongtaPrint", "onPrinterStatus: " + msg);
                if (!msg.isEmpty())toastUI("print status：" +msg);
                break;
            case 2://cash box status
                int scashbox = PrinterStatusPareseUtils.parseCashdrawerStatus(bytes);
                Log.i("rongtaPrint", "onClickStatus: " + scashbox);
                if (scashbox == 1) {
                    toastUI("Cashdrawer Open");
                } else if (scashbox == 0) {
                    toastUI("Cashdrawer Close");
                }
                break;
            case 3://Firmware version
                int msgLen=bytes.length;
                if (msgLen > 6) {
                    int l = msgLen-6;
                    byte[] subByteArr = subByteArr(bytes, 6, l);
                    msg= getByteArr2Str(subByteArr);
                   toastUI(msg);
                }
                break;

        }
    }

    /**
     * wifi 连接信息填写
     */
    private void showWifiChooseDialog() {
        AlertDialog.Builder dialog = new AlertDialog.Builder(this);
        dialog.setTitle(R.string.dialog_tip);

        View view = LayoutInflater.from(this).inflate(R.layout.dialog_wifi_config, null);
        final EditText et_wifi_ip = view.findViewById(R.id.et_wifi_ip);
        final EditText et_wifi_port = view.findViewById(R.id.et_wifi_port);

        String spIp = SPUtils.get(MainActivity.this, SP_KEY_IP, "192.168.").toString();
        String spPort = SPUtils.get(MainActivity.this, SP_KEY_PORT, "9100").toString();

        et_wifi_ip.setText(spIp);
        et_wifi_ip.setSelection(spIp.length());
        et_wifi_port.setText(spPort);

        dialog.setView(view);
        dialog.setPositiveButton(R.string.dialog_ok, new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                String ip = et_wifi_ip.getText().toString();
                String strPort = et_wifi_port.getText().toString();
                if (TextUtils.isEmpty(strPort)) {
                    strPort = "9100";
                }
                if (!TextUtils.isEmpty(ip)) {
                    SPUtils.put(MainActivity.this, SP_KEY_IP, ip);
                }
                if (!TextUtils.isEmpty(strPort)) {
                    SPUtils.put(MainActivity.this, SP_KEY_PORT, strPort);
                }
                configObj = new WiFiConfigBean(ip, Integer.parseInt(strPort));
                tv_device_selected.setText(configObj.toString());
                tv_device_selected.setTag(BaseEnum.HAS_DEVICE);
                isConfigPrintEnable(configObj);
            }
        });
        dialog.setNegativeButton(R.string.dialog_cancel, null);
        dialog.show();

    }
    @SuppressLint("MissingPermission")
    private void showBluetoothDeviceChooseDialog() {
        BluetoothDeviceChooseDialog bluetoothDeviceChooseDialog = new BluetoothDeviceChooseDialog();
        bluetoothDeviceChooseDialog.setOnDeviceItemClickListener(new BluetoothDeviceChooseDialog.onDeviceItemClickListener() {
            @Override
            public void onDeviceItemClick(BluetoothDevice device) {
                if (TextUtils.isEmpty(device.getName())) {
                    tv_device_selected.setText(device.getAddress());
                } else {
                    tv_device_selected.setText(device.getName() + " [" + device.getAddress() + "]");
                }
                configObj = new BluetoothEdrConfigBean(device);
                tv_device_selected.setTag(BaseEnum.HAS_DEVICE);
                isConfigPrintEnable(configObj);
            }
        });
        bluetoothDeviceChooseDialog.show(MainActivity.this.getFragmentManager(), null);
    }


    /**
     * 初始化设备列表
     */
    private void initDevice() {
        SerialPortFinder serialPortFinder = new SerialPortFinder();
        // 设备
        mDevices = serialPortFinder.getAllDevicesPath();
        if (mDevices.length == 0) {
            mDevices = new String[]{
                    getString(R.string.str_no_devices)
            };
        }
        // 波特率
        mBaudrates = getResources().getStringArray(R.array.baudrates);
        mDeviceIndex = mDevices.length - 1;
        mDeviceIndex = mDeviceIndex >= mDevices.length ? mDevices.length - 1 : mDeviceIndex;
        mBaudrateIndex = 0;
    }

    private void initSpinners(){
        ArrayAdapter<String> deviceAdapter =
                new ArrayAdapter<String>(this,R.layout.spinner_default_item,mDevices);
        deviceAdapter.setDropDownViewResource(R.layout.spinner_item);
        mSpComPath.setAdapter(deviceAdapter);
        mSpComPath.setOnItemSelectedListener(this);

        ArrayAdapter<String> baudrateAdapter =
                new ArrayAdapter<String>(this, R.layout.spinner_default_item, mBaudrates);
        baudrateAdapter.setDropDownViewResource(R.layout.spinner_item);
        mSpBps.setAdapter(baudrateAdapter);
        mSpBps.setOnItemSelectedListener(this);

        mSpComPath.setSelection(mDeviceIndex);
        mSpBps.setSelection(mBaudrateIndex);

    }

    /**
     * com设备选择
     */
    private void showCOMDeviceChooseDialog() {
        View comSelectView = View.inflate(this,R.layout.dialog_com_select,null);
        TextView title = comSelectView.findViewById(R.id.tv_dialog_title);
        title.setText("Title");
        mSpComPath = comSelectView.findViewById(R.id.sp_com_path);
        mSpBps = comSelectView.findViewById(R.id.sp_bps);
        initDevice();
        initSpinners();
        Button btn_cancle = comSelectView.findViewById(R.id.negativeButton);
        Button btn_comfirm = comSelectView.findViewById(R.id.positiveButton);
        final AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setView(comSelectView);
        final AlertDialog dialog = builder.create();
        dialog.setCanceledOnTouchOutside(false);
        dialog.show();

        btn_cancle.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                dialog.dismiss();
            }
        });

        btn_comfirm.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                tv_device_selected.setText(mDevices[mDeviceIndex]+" - "+mBaudrates[mBaudrateIndex]);
                tv_device_selected.setTag(BaseEnum.HAS_DEVICE);
                dialog.dismiss();
            }
        });
    }


    @Override
    public void onItemSelected(AdapterView<?> adapterView, View view, int i, long l) {
        switch (adapterView.getId()) {
            case R.id.sp_com_path:
                mDeviceIndex = i;
                mSerialPortConfigBean.setFile(new File(mDevices[mDeviceIndex]));
                break;
            case R.id.sp_bps:
                mBaudrateIndex = i;
                mSerialPortConfigBean.setBaudrate(Integer.parseInt(mBaudrates[mBaudrateIndex]));
                break;
        }
    }

    @Override
    public void onNothingSelected(AdapterView<?> adapterView) {

    }

    /**
     * usb设备选择
     */
    private void showUSBDeviceChooseDialog() {
        final UsbDeviceChooseDialog usbDeviceChooseDialog = new UsbDeviceChooseDialog();
        usbDeviceChooseDialog.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> parent, View view, int position, long id) {
                UsbDevice mUsbDevice = (UsbDevice) parent.getAdapter().getItem(position);
                PendingIntent mPermissionIntent;
                if(Build.VERSION.SDK_INT>=Build.VERSION_CODES.S) {//zac add android 12 20230403
                    mPermissionIntent = PendingIntent.getBroadcast(MainActivity.this, 0,
                            new Intent(MainActivity.this.getApplicationInfo().packageName), PendingIntent.FLAG_IMMUTABLE);
                }else {
                    mPermissionIntent = PendingIntent.getBroadcast(MainActivity.this, 0,
                            new Intent(MainActivity.this.getApplicationInfo().packageName), 0);

                }
                tv_device_selected.setText(getString(R.string.adapter_usbdevice) + mUsbDevice.getDeviceId()); //+ (position + 1));
                configObj = new UsbConfigBean(BaseApplication.getInstance(), mUsbDevice, mPermissionIntent);
                tv_device_selected.setTag(BaseEnum.HAS_DEVICE);
                isConfigPrintEnable(configObj);
                usbDeviceChooseDialog.dismiss();
            }
        });
        usbDeviceChooseDialog.show(getFragmentManager(), null);
    }

    /**
     * 设置是否可进行打印操作
     *
     * @param isEnable
     */
    private void setPrintEnable(boolean isEnable) {
        btn_selftest_print.setEnabled(isEnable);
        btn_txt_print.setEnabled(isEnable);
        btn_img_print.setEnabled(isEnable);
        btn_template_print.setEnabled(isEnable);
        btn_barcode_print.setEnabled(isEnable);
        btn_connect.setEnabled(!isEnable);
        btn_disConnect.setEnabled(isEnable);
        btn_beep.setEnabled(isEnable);
        btn_all_cut.setEnabled(isEnable);
        btn_cash_box.setEnabled(isEnable);
        btn_wifi_setting.setEnabled(isEnable);
        btn_wifi_ipdhcp.setEnabled(isEnable);
        btn_cmd_test.setEnabled(isEnable);
        btn_test.setEnabled(isEnable);
//        btn_label_setting.setEnabled(isEnable);
        btn_print_status.setEnabled(isEnable);
        btn_print_tsc_density.setEnabled(isEnable);
        btn_setXYPoint.setEnabled(isEnable);
        btn_read_optocoupler.setEnabled(isEnable);
        btn_learnNoPaper.setEnabled(isEnable);
        btn_FactoryDataReset.setEnabled(isEnable);
        btn_LearnLabel.setEnabled(isEnable);
        btn_cash_box_status.setEnabled(isEnable);
        btn_print_update.setEnabled(isEnable);
        btn_print_firmware_version.setEnabled(isEnable);
    }

    private void isConfigPrintEnable(Object configObj) {
        if (isInConnectList(configObj)) {
            setPrintEnable(true);
        } else {
            setPrintEnable(false);
        }
    }

    private boolean isInConnectList(Object configObj) {
        boolean isInList = false;
        for (int i = 0; i < printerInterfaceArrayList.size(); i++) {
            PrinterInterface printerInterface = printerInterfaceArrayList.get(i);
            if (configObj.toString().equals(printerInterface.getConfigObject().toString())) {
                if (printerInterface.getConnectState() == ConnectStateEnum.Connected) {
                    isInList = true;
                    break;
                }
            }
        }
        return isInList;
    }

    @Override
    public void onBackPressed() {
        super.onBackPressed();
        System.exit(0);//完全退出应用，关闭进程
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if(requestCode == REQUEST_ENABLE_BT){
            if(requestCode == RESULT_OK){
                showBluetoothDeviceChooseDialog();
            }
        }
    }

    @Override
    public boolean onLongClick(View view) {
        if (checkedConType == BaseEnum.CON_WIFI) {
            switch (view.getId()) {
                case R.id.tv_device_selected:
                    showWifiChooseDialog();
                    break;
            }
        }
        return true;
    }
    private void toastUI(String msg){
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                ToastUtil.show(MainActivity.this,  msg);
            }
        });
    }

}
