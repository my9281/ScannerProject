using System.IO;
using System.Text.Json;
using Scanner.Server.Model;

namespace WPF.Camara;

public sealed class RepairStartSession : IDisposable
{
    private readonly string path;
    private readonly FileStream guard;
    public string Sn { get; private set; } = "";
    public string Sku { get; private set; } = "";
    public DateTimeOffset? StartedAt { get; private set; }
    public RepairScan? Pending { get; private set; }
    public bool CanSubmit => ValidCode(Sn) && ValidCode(Sku) && StartedAt is not null;

    public RepairStartSession(string path)
    {
        this.path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        guard = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (!File.Exists(path)) return;
            Pending = JsonSerializer.Deserialize<RepairScan>(File.ReadAllText(path))
                ?? throw new InvalidDataException("待上传记录为空。请保留文件并联系管理员。");
            if (Pending.ScanId == Guid.Empty || !ValidCode(Pending.Sn) || !ValidCode(Pending.Sku) || Pending.StartedAt == default)
                throw new InvalidDataException("待上传记录格式无效。请保留文件并联系管理员。");
            Sn = Pending.Sn;
            Sku = Pending.Sku;
            StartedAt = Pending.StartedAt;
        }
        catch { guard.Dispose(); throw; }
    }

    public static bool ValidCode(string? value) => value?.Trim() is { Length: >= 1 and <= 50 } text && !text.Any(char.IsControl);

    public void Update(string sn, string sku, DateTimeOffset now)
    {
        if (Pending is not null) throw new InvalidOperationException("记录已待上传，请先重试确认。");
        Sn = sn;
        Sku = sku;
        if (string.IsNullOrWhiteSpace(sn) && string.IsNullOrWhiteSpace(sku)) StartedAt = null;
        else StartedAt ??= now;
    }

    public RepairScan PrepareUpload()
    {
        if (Pending is not null) return Pending;
        if (!CanSubmit) throw new InvalidOperationException("SN 和 SKU 必须各为 1–50 位，且不能包含控制字符。");
        var scan = new RepairScan(Guid.NewGuid(), Sn.Trim(), Sku.Trim(), StartedAt!.Value);
        using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, scan);
            stream.Flush(flushToDisk: true);
        }
        File.Move(path + ".tmp", path, overwrite: true);
        Pending = scan;
        return scan;
    }

    public void Confirm(Guid scanId)
    {
        if (Pending?.ScanId != scanId) throw new InvalidOperationException("服务器确认编号不匹配，记录已保留。");
        File.Delete(path);
        Pending = null;
        Sn = Sku = "";
        StartedAt = null;
    }

    public void Dispose() => guard.Dispose();
}
