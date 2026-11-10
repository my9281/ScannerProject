using System.Text.Json;
using Scanner.Server.Model;

namespace Scanner.BreezaryStar.AndroidScanner.Services;

public sealed record CachedInboundScan(BreezaryInboundItem Item, DateTimeOffset? UploadedAt = null)
{
    public string Display => $"{Item.Barcode}\n缓存位置 {Item.CacheLocation} · {Item.ScannedAt.ToLocalTime():MM-dd HH:mm:ss} · {(UploadedAt is null ? "待上传" : "已上传")}";
}

/// <summary>Each scan is written atomically to its own file before success is reported.</summary>
public sealed class InboundCache(string directory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<Guid, CachedInboundScan> records = new();
    private bool loaded;
    private Guid deviceId;

    private async Task LoadAsync(CancellationToken ct)
    {
        if (loaded) return;
        Directory.CreateDirectory(directory);
        var identity = Path.Combine(directory, "device-id");
        var files = Directory.GetFiles(directory, "*.json");
        if (File.Exists(identity))
        {
            if (!Guid.TryParse(await File.ReadAllTextAsync(identity, ct), out deviceId) || deviceId == Guid.Empty)
                throw new InvalidDataException("本地设备标识损坏，扫描缓存未清除，请联系管理员。");
        }
        else
        {
            if (files.Length > 0) throw new InvalidDataException("设备标识缺失，扫描缓存未清除，请联系管理员。");
            deviceId = Guid.NewGuid();
            await WriteAtomicAsync(identity, deviceId.ToString("D"), ct);
        }
        var restored = new Dictionary<Guid, CachedInboundScan>();
        foreach (var file in files)
        {
            var row = JsonSerializer.Deserialize<CachedInboundScan>(await File.ReadAllTextAsync(file, ct))
                ?? throw new InvalidDataException("本地记录损坏，停止读取以保护缓存。");
            BreezaryInboundValidation.Validate([row.Item]);
            if (row.Item.DeviceId != deviceId || Path.GetFileNameWithoutExtension(file) != row.Item.ScanId.ToString("D")
                || !restored.TryAdd(row.Item.ScanId, row)) throw new InvalidDataException("本地扫描身份不匹配，缓存未清除。");
        }
        records.Clear(); foreach (var pair in restored) records.Add(pair.Key, pair.Value);
        loaded = true;
    }

    public async Task<CachedInboundScan> AddAsync(int location, string value, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            var item = new BreezaryInboundItem(Guid.NewGuid(), deviceId, location, value.TrimEnd('\r', '\n'), DateTimeOffset.UtcNow);
            BreezaryInboundValidation.Validate([item]);
            var row = new CachedInboundScan(item);
            await SaveAsync(row, ct);
            records.Add(item.ScanId, row);
            return row;
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<CachedInboundScan>> SnapshotAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { await LoadAsync(ct); return records.Values.OrderByDescending(x => x.Item.ScannedAt).ToArray(); }
        finally { gate.Release(); }
    }

    public async Task AcknowledgeAsync(IReadOnlyList<Guid> ids, DateTimeOffset time, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            if (ids.Any(id => !records.ContainsKey(id))) throw new InvalidOperationException("服务器确认了未知记录，缓存未清除。");
            foreach (var id in ids)
            {
                var row = records[id] with { UploadedAt = time };
                await SaveAsync(row, ct); records[id] = row;
            }
        }
        finally { gate.Release(); }
    }

    private Task SaveAsync(CachedInboundScan row, CancellationToken ct) =>
        WriteAtomicAsync(Path.Combine(directory, row.Item.ScanId.ToString("D") + ".json"), JsonSerializer.Serialize(row), ct);

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken ct)
    {
        var pending = path + ".pending";
        await using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct); stream.Flush(flushToDisk: true);
        }
        File.Move(pending, path, overwrite: true);
    }
}
