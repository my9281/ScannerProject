using System.Text.Json;
using Scanner.Server.Model;

namespace Scanner.ShippingCowAndroid.Services;

public sealed record CarrierReceiptLocalRow(CarrierReceiptScan Scan, bool Uploaded, string? Warning = null);

/// <summary>Atomic durable outbox. Credentials are stored separately by the platform secure store.</summary>
public sealed class CarrierReceivingQueue
{
    private readonly string path;
    private State state;
    public IReadOnlyList<CarrierReceiptScan> Pending => state.Pending.AsReadOnly();
    public IReadOnlyList<CarrierReceiptLocalRow> Rows => state.Rows!.AsReadOnly();
    public CarrierReceiptContext? Context => state.Context;
    public Guid DeviceId => state.DeviceId;
    public Guid BatchId => state.BatchId;
    public string BatchNumber => state.BatchNumber;
    public bool IsUploading { get; private set; }
    public CarrierReceivingQueue(string path)
    {
        this.path = path;
        if (File.Exists(path))
        {
            state = JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? throw new InvalidDataException("收货缓存无法读取，请保留文件并联系管理员。");
            if (state.Version != 1 || state.DeviceId == Guid.Empty || state.BatchId == Guid.Empty || state.Pending is null || state.Seen is null
                || state.Pending.Select(s => s.ScanId).Distinct().Count() != state.Pending.Count)
                throw new InvalidDataException("收货缓存不完整，未覆盖原文件。");
            // Upgrade older outboxes without changing their pending scan IDs or ownership.
            state = state with { Rows = state.Rows ?? state.Pending.Select(s => new CarrierReceiptLocalRow(s, false)).ToList() };
            try
            {
                foreach (var item in state.Pending)
                {
                    ValidateLocal(item, DateTimeOffset.MaxValue.AddMinutes(-5));
                    if (state.Context is null ? string.IsNullOrWhiteSpace(state.CaptureUsername) || string.IsNullOrWhiteSpace(state.CaptureServer) || item.DomainId != 0 || item.OperatorUserId != 0
                        : item.DomainId != state.Context.DomainId || item.OperatorUserId != state.Context.UserId || item.SiteId != state.Context.SiteId)
                        throw new InvalidDataException("收货缓存账号范围不一致，未覆盖原文件。");
                }
            }
            catch (ArgumentException)
            {
                // User requested discarding incompatible historical scans instead of blocking the page.
                Commit(state with { BatchId = Guid.NewGuid(), BatchNumber = NewBatchNumber(), Pending = [], Seen = [], Rows = [] });
            }
        }
        else
        {
            state = new(1, Guid.NewGuid(), Guid.NewGuid(), NewBatchNumber(), null, [], [], []);
            Persist(state);
        }
    }
    private static string NewBatchNumber() => DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
    private sealed record State(int Version, Guid DeviceId, Guid BatchId, string BatchNumber, CarrierReceiptContext? Context,
        List<CarrierReceiptScan> Pending, List<string> Seen, List<CarrierReceiptLocalRow>? Rows = null, string? CaptureUsername = null, string? CaptureServer = null);
    public void PrepareCapture(string server, string username)
    {
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(username)) throw new ArgumentException("收货配置无效。");
        if (state.Context is null && state.Pending.Count > 0 && (state.CaptureServer != server || !string.Equals(state.CaptureUsername, username, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("请使用原收货配置上传待处理记录。");
        Commit(state with { CaptureUsername = username, CaptureServer = server });
    }
    private static void ValidateLocal(CarrierReceiptScan item, DateTimeOffset now) =>
        CarrierReceiptValidation.Validate(item.DomainId == 0 && item.OperatorUserId == 0 ? item with { DomainId = 1, OperatorUserId = 1 } : item, now);
    private void Persist(State next)
    {
        string? directory = Path.GetDirectoryName(path); if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, next); file.Flush(flushToDisk: true);
        }
        File.Move(path + ".tmp", path, true);
    }
    private void Commit(State next) { Persist(next); state = next; }
    public void Configure(CarrierReceiptContext context)
    {
        if (IsUploading) throw new InvalidOperationException("正在上传，请稍候。");
        if (context.UserId == 0 || context.DomainId <= 0 || string.IsNullOrWhiteSpace(context.SiteId)) throw new ArgumentException("登录信息无效。");
        if (state.Context is null && state.Pending.Count > 0)
        {
            if (state.CaptureServer != context.Server || !string.Equals(state.CaptureUsername, context.Username, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("登录账号与离线收货配置不一致。");
            CarrierReceiptScan Bind(CarrierReceiptScan s) => s with { DomainId = context.DomainId, OperatorUserId = context.UserId, SiteId = context.SiteId };
            Commit(state with { Context = context, Pending = state.Pending.Select(Bind).ToList(), Rows = state.Rows!.Select(r => r with { Scan = Bind(r.Scan) }).ToList() });
            return;
        }
        bool same = state.Context is { } old && old.UserId == context.UserId && old.DomainId == context.DomainId && old.SiteId == context.SiteId && old.Server == context.Server;
        if (!same && state.Pending.Count > 0 && state.Context is { } original && original.UserId == context.UserId && original.DomainId == context.DomainId && original.Server == context.Server)
        {
            // Refresh credentials for the same person without reassigning an old site's offline records.
            Commit(state with { Context = original with { Username = context.Username } });
            return;
        }
        if (!same && state.Pending.Count > 0) throw new InvalidOperationException("还有其他账号或站点的待上传收货，请使用原账号上传后再切换。");
        bool sameOwner = state.Context is { } prior && prior.UserId == context.UserId && prior.DomainId == context.DomainId && prior.Server == context.Server;
        Commit(same ? state with { Context = context } : state with { Context = context, BatchId = Guid.NewGuid(), BatchNumber = NewBatchNumber(), Seen = [], Rows = sameOwner ? state.Rows : [] });
    }
    public string? Add(string raw, DateTimeOffset time)
    {
        var context = state.Context;
        if (context is null && string.IsNullOrWhiteSpace(state.CaptureUsername)) throw new InvalidOperationException("缺少收货配置。");
        string? warning = CarrierReceiptValidation.ScanWarning(raw, out var detectedCarrier, out string number);
        string carrier = detectedCarrier ?? "";
        string key = carrier + ":" + number;
        var item = new CarrierReceiptScan(Guid.NewGuid(), state.DeviceId, state.BatchId, state.BatchNumber, context?.DomainId ?? 0,
            context?.UserId ?? 0, context?.SiteId ?? "unassigned", carrier, number, raw, time);
        if (warning is null) ValidateLocal(item, DateTimeOffset.UtcNow);
        Commit(state with { Pending = warning is null ? [.. state.Pending, item] : state.Pending,
            Seen = [.. state.Seen.TakeLast(4999), key], Rows = [.. state.Rows!, new(item, false, warning)] });
        return warning;
    }
    public void NewBatch()
    {
        if (IsUploading || state.Pending.Count > 0) throw new InvalidOperationException("请先上传当前批次，未确认记录不能清空。");
        Commit(state with { BatchId = Guid.NewGuid(), BatchNumber = NewBatchNumber(), Seen = [] });
    }
    public void ClearHistory()
    {
        if (IsUploading) throw new InvalidOperationException("正在上传，请稍候。");
        Commit(state with { BatchId = Guid.NewGuid(), BatchNumber = NewBatchNumber(), Pending = [], Seen = [], Rows = [] });
    }
    public void Delete(Guid scanId)
    {
        if (IsUploading) throw new InvalidOperationException("正在上传，请稍候。");
        Commit(state with { Pending = state.Pending.Where(s => s.ScanId != scanId).ToList(),
            Rows = state.Rows!.Where(r => r.Scan.ScanId != scanId).ToList() });
    }
    public async Task<int> Upload(CarrierReceiptContext context, Func<CarrierReceiptBatch, CancellationToken, Task<CarrierReceiptAcknowledgement>> send, CancellationToken ct)
    {
        if (IsUploading) throw new InvalidOperationException("正在上传，请稍候。");
        if (state.Context is not { } owner || owner.UserId != context.UserId || owner.DomainId != context.DomainId || owner.Server != context.Server)
            throw new InvalidOperationException("当前登录与本地记录不匹配，请使用原账号和站点。");
        int confirmed = 0; IsUploading = true;
        try
        {
            foreach (var chunk in state.Pending.ToArray().Chunk(100))
            {
                ct.ThrowIfCancellationRequested();
                var ack = await send(new(chunk), ct);
                var expected = chunk.Select(x => x.ScanId).ToHashSet();
                if (ack.AcceptedScanIds is null || ack.AcceptedScanIds.Count != chunk.Length || !expected.SetEquals(ack.AcceptedScanIds))
                    throw new InvalidOperationException("服务器确认不完整，本批记录已保留，请重试。");
                // If persistence fails, keep in-memory entries as well; safe replay uses the original IDs.
                var rows = state.Rows!.Select(r => expected.Contains(r.Scan.ScanId) ? r with { Uploaded = true } : r).ToList();
                var recentUploaded = rows.Where(r => r.Uploaded).TakeLast(5000).Select(r => r.Scan.ScanId).ToHashSet();
                Commit(state with { Pending = state.Pending.Where(x => !expected.Contains(x.ScanId)).ToList(),
                    Rows = rows.Where(r => !r.Uploaded || recentUploaded.Contains(r.Scan.ScanId)).ToList() });
                confirmed += chunk.Length;
            }
            return confirmed;
        }
        finally { IsUploading = false; }
    }
}
