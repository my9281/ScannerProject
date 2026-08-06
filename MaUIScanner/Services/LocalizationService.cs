using System.Globalization;

namespace MaUIScanner.Services;

public sealed class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new()
    {
        ["zh-CN"] = new() { ["Title"] = "幽梦运单之星扫描系统", ["Scan"] = "扫描输入", ["Hint"] = "扫码后按 Enter 自动处理", ["AutoPrint"] = "自动打印", ["Print"] = "处理扫描", ["OpenLog"] = "打开扫描日志", ["Import"] = "导入紧急 CSV", ["Runtime"] = "运行信息", ["Status"] = "处理状态", ["Waiting"] = "等待扫描", ["Printer"] = "默认打印机：{0}", ["Count"] = "本次扫描：{0}", ["OidFirst"] = "识别为 OID，已记录，本次不打印。", ["OidPrint"] = "OID 再次扫描，已打印：{0}", ["Printed"] = "已记录并打印：{0}", ["Saved"] = "已记录但未打印：{0}", ["Offline"] = "本地离线模式", ["Language"] = "zh-CN" },
        ["en-US"] = new() { ["Title"] = "YM-Star Scanner System", ["Scan"] = "Scan input", ["Hint"] = "Press Enter after scanning", ["AutoPrint"] = "Auto print", ["Print"] = "Process scan", ["OpenLog"] = "Open scan log", ["Import"] = "Import urgent CSV", ["Runtime"] = "Runtime information", ["Status"] = "Processing status", ["Waiting"] = "Waiting for scan", ["Printer"] = "Default printer: {0}", ["Count"] = "Scanned this session: {0}", ["OidFirst"] = "OID recorded. Not printed this time.", ["OidPrint"] = "OID scanned again and printed: {0}", ["Printed"] = "Recorded and printed: {0}", ["Saved"] = "Recorded without printing: {0}", ["Offline"] = "Local offline mode", ["Language"] = "en-US" },
        ["es-ES"] = new() { ["Title"] = "Sistema de escaneo YM-Star", ["Scan"] = "Entrada de escaneo", ["Hint"] = "Pulse Enter después de escanear", ["AutoPrint"] = "Impresión automática", ["Print"] = "Procesar escaneo", ["OpenLog"] = "Abrir registro", ["Import"] = "Importar CSV urgente", ["Runtime"] = "Información de ejecución", ["Status"] = "Estado del proceso", ["Waiting"] = "Esperando escaneo", ["Printer"] = "Impresora predeterminada: {0}", ["Count"] = "Escaneados en esta sesión: {0}", ["OidFirst"] = "OID registrado. No se imprimió esta vez.", ["OidPrint"] = "OID escaneado de nuevo e impreso: {0}", ["Printed"] = "Registrado e impreso: {0}", ["Saved"] = "Registrado sin imprimir: {0}", ["Offline"] = "Modo local sin conexión", ["Language"] = "es-ES" }
    };
    public string Language { get; private set; } = "zh-CN";
    public event EventHandler? Changed;
    public string Get(string key) => _resources[Language].TryGetValue(key, out string? value) ? value : key;
    public string Format(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, Get(key), values);
    public void Change(string language)
    {
        if (!_resources.ContainsKey(language)) language = "zh-CN";
        Language = language;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
