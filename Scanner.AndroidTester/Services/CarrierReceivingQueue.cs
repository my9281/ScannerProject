using System.Text.Json;
using Scanner.Server.Model;

namespace Scanner.AndroidTester.Services;

/// <summary>Atomic durable outbox. Credentials are stored separately by the platform secure store.</summary>
public sealed class CarrierReceivingQueue
{
    private readonly string path;
    private State state;
    public IReadOnlyList<CarrierReceiptScan> Pending => state.Pending.AsReadOnly();
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
                || state.Pending.Count > 5000 || state.Pending.Select(s => s.ScanId).Distinct().Count() != state.Pending.Count)
                throw new InvalidDataException("收货缓存不完整，未覆盖原文件。");
            foreach (var item in state.Pending)
            {
                CarrierReceiptValidation.Validate(item, DateTimeOffset.MaxValue.AddMinutes(-5));
                if (state.Context is null || item.DomainId != state.Context.DomainId || item.OperatorUserId != state.Context.UserId || item.SiteId != state.Context.SiteId)
                    throw new InvalidDataException("收货缓存账号范围不一致，未覆盖原文件。");
            }
        }
        else
        {
            state = new(1, Guid.NewGuid(), Guid.NewGuid(), NewBatchNumber(), null, [], []);
            Persist(state);
        }
    }
    private static string NewBatchNumber() => DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
    private sealed record State(int Version, Guid DeviceId, Guid BatchId, string BatchNumber, CarrierReceiptContext? Context,
        List<CarrierReceiptScan> Pending, List<string> Seen);
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
        bool same = state.Context is { } old && old.UserId == context.UserId && old.DomainId == context.DomainId && old.SiteId == context.SiteId && old.Server == context.Server;
        if (!same && state.Pending.Count > 0 && state.Context is { } original && original.UserId == context.UserId && original.DomainId == context.DomainId && original.Server == context.Server)
        {
            // Refresh credentials for the same person without reassigning an old site's offline records.
            Commit(state with { Context = original with { Username = context.Username } });
            return;
        }
        if (!same && state.Pending.Count > 0) throw new InvalidOperationException("还有其他账号或站点的待上传收货，请使用原账号上传后再切换。");
        Commit(same ? state with { Context = context } : state with { Context = context, BatchId = Guid.NewGuid(), BatchNumber = NewBatchNumber(), Seen = [] });
    }
    public void Add(string carrier, string raw, DateTimeOffset time)
    {
        if (IsUploading) throw new InvalidOperationException("正在上传，请稍候。");
        var context = state.Context ?? throw new InvalidOperationException("请先联网登录并确认站点，之后可离线收货。");
        string number = CarrierReceiptValidation.NormalizeNumber(raw);
        string key = carrier + ":" + number;
        if (state.Seen.Contains(key, StringComparer.Ordinal)) throw new ArgumentException("本批已扫描该运单，未重复记录。");
        if (state.Pending.Count >= 5000) throw new InvalidOperationException("待上传已达 5000 条，请先上传。");
        if (state.Seen.Count >= 5000) throw new InvalidOperationException("本批已达 5000 条，请上传后开始新批次。");
        var item = new CarrierReceiptScan(Guid.NewGuid(), state.DeviceId, state.BatchId, state.BatchNumber, context.DomainId,
            context.UserId, context.SiteId, carrier, number, raw, time);
        CarrierReceiptValidation.Validate(item, DateTimeOffset.UtcNow);
        Commit(state with { Pending = [.. state.Pending, item], Seen = [.. state.Seen, key] });
    }
    public void NewBatch()
    {
        if (IsUploading || state.Pending.Count > 0) throw new InvalidOperationException("请先上传当前批次，未确认记录不能清空。");
        Commit(state with { BatchId = Guid.NewGuid(), BatchNumber = NewBatchNumber(), Seen = [] });
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
                Commit(state with { Pending = state.Pending.Where(x => !expected.Contains(x.ScanId)).ToList() });
                confirmed += chunk.Length;
            }
            return confirmed;
        }
        finally { IsUploading = false; }
    }
}
