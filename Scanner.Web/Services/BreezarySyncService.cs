using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace Scanner.Web.Services;

public sealed class BreezarySyncService(IBreezaryStore store, IBreezaryRemote remote, IOptions<TencentDocsOptions> options)
{
    private readonly string target = BreezaryMerge.Target(options.Value);

    public Task<BreezaryLocalPage> ListAsync(int offset, int limit, CancellationToken ct)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ApiException(400, "offset 不能小于 0，limit 必须为 1–100。");
        return store.ListAsync(target, offset, limit, ct);
    }

    public async Task<BreezarySyncResult> PullAsync(CancellationToken ct)
    {
        await using var lease = await store.AcquireAsync(target, ct);
        // Complete remote pagination before opening the atomic database write transaction.
        var existing = (await store.ReadAllAsync(target, ct)).ToDictionary(x => x.RecordId, StringComparer.Ordinal);
        var incoming = await remote.ReadAllAsync(ct);
        var rows = incoming.Select(row => existing.TryGetValue(row.RecordId, out var old)
            ? old with { Remote = row.Remote, LastPulledAt = DateTime.UtcNow, Version = old.Version + 1 }
            : row with { LastPulledAt = DateTime.UtcNow, Version = 1 }).ToArray();
        await store.SaveAsync(target, rows, ct);
        return new(rows.Length, 0, 0, 0);
    }

    public async Task<JsonObject> EditAsync(string recordId, BreezaryEditRequest request, CancellationToken ct)
    {
        await using var lease = await store.AcquireAsync(target, ct);
        var row = (await store.ReadAllAsync(target, ct)).FirstOrDefault(x => x.RecordId == recordId)
            ?? throw new ApiException(404, "本地记录不存在，请先同步入库。");
        var updated = BreezaryMerge.Edit(row, request);
        await store.SaveAsync(target, [updated], ct);
        return BreezaryMerge.ToJson(updated);
    }

    public async Task<BreezarySyncResult> PushAsync(CancellationToken ct)
    {
        await using var lease = await store.AcquireAsync(target, ct);
        var pending = (await store.ReadAllAsync(target, ct)).Where(x => x.Pending.Count > 0).ToArray();
        if (pending.Length == 0) return new(0, 0, 0, 0);
        // Compare only locally edited fields; unrelated Tencent edits remain intact.
        var current = (await remote.ReadAllAsync(ct)).ToDictionary(x => x.RecordId, StringComparer.Ordinal);
        int pushed = 0, conflicts = 0, failed = 0;
        foreach (var row in pending)
        {
            ct.ThrowIfCancellationRequested();
            if (!current.TryGetValue(row.RecordId, out var latest) || BreezaryMerge.Conflicts(row, latest.Remote))
            {
                await store.SaveAsync(target, [row with { Remote = latest?.Remote ?? row.Remote,
                    SyncStatus = "conflict", LastError = "腾讯记录已删除或同一字段已被修改，请核对后重新编辑。", Version = row.Version + 1 }], ct);
                conflicts++;
                continue;
            }
            try
            {
                var allApplied = row.Pending.All(pair => BreezaryMerge.Equivalent(latest.Remote[pair.Key], pair.Value));
                var updated = allApplied ? latest.Remote : BreezaryMerge.Merge(latest.Remote,
                    await remote.UpdateAsync(row.RecordId, row.Pending, ct));
                await store.SaveAsync(target, [row with { Remote = updated, Pending = new(), Baseline = new(),
                    SyncStatus = "clean", LastError = "", LastPushedAt = DateTime.UtcNow, Version = row.Version + 1 }], ct);
                pushed++;
            }
            catch (ApiException ex)
            {
                await store.SaveAsync(target, [row with { SyncStatus = "error", LastError = ex.Message[..Math.Min(1000, ex.Message.Length)], Version = row.Version + 1 }], ct);
                failed++;
            }
        }
        return new(0, pushed, conflicts, failed);
    }
}
