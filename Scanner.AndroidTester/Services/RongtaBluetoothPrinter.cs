#if ANDROID
using Android.Bluetooth;
using Com.RT.Printerlibrary.Bean;
using Com.RT.Printerlibrary.Cmd;
using Com.RT.Printerlibrary.Enumerate;
using Com.RT.Printerlibrary.Factory.Connect;
using Com.RT.Printerlibrary.Factory.Printer;
using Com.RT.Printerlibrary.Setting;
using Com.RT.Printerlibrary.Utils;

namespace Scanner.AndroidTester.Services;

public sealed class PrinterBluetoothPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(31)
            ? new[] { (Android.Manifest.Permission.BluetoothConnect, true), (Android.Manifest.Permission.BluetoothScan, true) }
            : new[] { (Android.Manifest.Permission.Bluetooth, false), (Android.Manifest.Permission.BluetoothAdmin, false) };
}

public static class RongtaBluetoothPrinter
{
    private const string AddressKey = "rongta-printer-address";
    private const string ProtocolKey = "rongta-printer-protocol";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static void ResetSelection() { Preferences.Remove(AddressKey); Preferences.Remove(ProtocolKey); }

    public static async Task<bool> PrintAsync(Page page, string sn)
    {
        if (!await Gate.WaitAsync(0)) throw new InvalidOperationException("已有打印任务正在发送，请稍候。");
        try
        {
            if (await Permissions.RequestAsync<PrinterBluetoothPermission>() != PermissionStatus.Granted)
                throw new InvalidOperationException("需要允许“附近设备”权限才能连接蓝牙打印机。");
            var manager = (BluetoothManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.BluetoothService);
            var adapter = manager?.Adapter ?? throw new InvalidOperationException("设备不支持蓝牙。");
            if (!adapter.IsEnabled) throw new InvalidOperationException("请先在系统设置中打开蓝牙。");
            var devices = adapter.BondedDevices?.OrderBy(x => x.Name).ToArray() ?? Array.Empty<BluetoothDevice>();
            if (devices.Length == 0)
            {
                if (await page.DisplayAlertAsync("配对打印机", "请先在系统蓝牙设置中配对 RP425，再返回点击打印。", "打开蓝牙设置", "取消"))
                    Platform.CurrentActivity?.StartActivity(new Android.Content.Intent(Android.Provider.Settings.ActionBluetoothSettings));
                return false;
            }
            string address = Preferences.Get(AddressKey, "");
            var device = devices.FirstOrDefault(x => x.Address == address);
            if (device is null)
            {
                var names = devices.Select(x => $"{x.Name ?? "蓝牙设备"} ({x.Address})").ToArray();
                var choice = await page.DisplayActionSheetAsync("选择 RP425 打印机", "取消", null, names);
                int index = Array.IndexOf(names, choice);
                if (index < 0) return false;
                device = devices[index];
                address = device.Address!;
            }
            string protocol = Preferences.Get(ProtocolKey, "");
            if (protocol is not ("TSPL" or "ZPL"))
            {
                var choice = await page.DisplayActionSheetAsync("按打印机自测页选择指令类型", "取消", null, "TSPL（国内版）", "ZPL（国际版）");
                if (choice == "TSPL（国内版）") protocol = "TSPL";
                else if (choice == "ZPL（国际版）") protocol = "ZPL";
                else return false;
            }
            Preferences.Set(AddressKey, address);
            Preferences.Set(ProtocolKey, protocol);
            // Construct SDK interfaces on the UI thread: vendor implementation uses Android handlers.
            using var factory = new LabelPrinterFactory();
            using var printer = factory.Create() ?? throw new InvalidOperationException("无法初始化容大 SDK。");
            using var bluetoothFactory = new BluetoothFactory();
            using var connection = bluetoothFactory.Create() ?? throw new InvalidOperationException("无法初始化蓝牙接口。");
            using var configuration = new BluetoothEdrConfigBean(device);
            using var listener = new ConnectionListener();
            connection.ConfigObject = configuration;
            printer.PrinterInterface = connection;
            printer.SetConnectListener(listener);
            bool sending = false;
            string stage = "连接蓝牙";
            try
            {
                // No automatic retry: a transport failure may occur after paper has already printed.
                printer.Connect(configuration);
                await listener.Connected.Task.WaitAsync(TimeSpan.FromSeconds(25));
                stage = "生成标签图片";
                using var bitmap = await Task.Run(() => AndroidLabelPrinter.CreatePrinterBitmap(sn));
                stage = "生成标签指令";
                byte[] data = await Task.Run(() =>
                {
                    using Cmd command = (protocol == "TSPL" ? new TscFactory().Create() : new ZplFactory().Create())
                        ?? throw new InvalidOperationException("无法创建标签指令。");
                    using var size = new LableSizeBean(102, 152);
                    using var common = new CommonSetting { LableSizeBean = size, PrintDirection = PrintDirection.Normal };
                    if (protocol == "TSPL") common.LabelGap = 3;
                    using var position = new Position(0, 0);
                    using var image = new BitmapSetting { PrintPostion = position, BimtapLimitWidth = 816, BmpPrintMode = BmpPrintMode.ModeSingleFast };
                    command.Append(command.GetHeaderCmd());
                    command.Append(command.GetCommonSettingCmd(common));
                    command.Append(command.GetBitmapCmd(image, bitmap));
                    command.Append(command.GetPrintCopies(1));
                    if (protocol == "ZPL") command.Append(command.GetEndCmd());
                    return command.GetAppendCmds() ?? throw new InvalidOperationException("标签指令为空。");
                });
                sending = true;
                stage = "发送标签数据";
                Android.Util.Log.Info("ScannerPrint", $"Protocol={protocol}; bytes={data.Length}; sending");
                printer.WriteMsgAsync(data);
                await listener.Written.Task.WaitAsync(TimeSpan.FromSeconds(60));
                return true;
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException(sending ? "发送超时，可能已部分发送。请先检查是否出纸，再决定是否重试。" : "蓝牙连接超时，请确认打印机开机、距离正常且未被其他应用占用。");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("ScannerPrint", $"Stage={stage}; protocol={protocol}; {ex}");
                throw new InvalidOperationException($"{stage}失败（{protocol}）：{ex.Message}", ex);
            }
            finally
            {
                printer.SetConnectListener(null);
                printer.DisConnect();
            }
        }
        finally { Gate.Release(); }
    }
    private sealed class ConnectionListener : Java.Lang.Object, IConnectListener
    {
        public TaskCompletionSource<bool> Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnPrinterConnected(Java.Lang.Object? config) => Connected.TrySetResult(true);
        public void OnPrinterWritecompletion(Java.Lang.Object? config) => Written.TrySetResult(true);
        public void OnPrinterDisconnect(Java.Lang.Object? config)
        {
            Connected.TrySetException(new IOException("蓝牙连接失败或已断开。"));
            Written.TrySetException(new IOException("蓝牙连接断开，无法确认数据已完整发送。"));
        }
    }
}
#endif
