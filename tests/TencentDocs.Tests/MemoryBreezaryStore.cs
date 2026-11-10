using Scanner.Web.Services;
using System.Text.Json.Nodes;

sealed class MemoryBreezaryStore : IBreezaryStore
{
    private readonly Dictionary<string, Dictionary<string, BreezaryRow>> targets = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool FailNextSave;
    public async Task<IAsyncDisposable> AcquireAsync(string target, CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) throw new ApiException(409, "busy");
        return new Lease(gate);
    }
    public Task<IReadOnlyList<BreezaryRow>> ReadAllAsync(string target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<BreezaryRow>>(targets.TryGetValue(target, out var rows) ? rows.Values.Select(Clone).ToArray() : []);
    }
    public async Task<BreezaryLocalPage> ListAsync(string target, int offset, int limit, CancellationToken ct)
    {
        var all = await ReadAllAsync(target, ct);
        return new(new JsonArray(all.Skip(offset).Take(limit).Select(x => (JsonNode)BreezaryMerge.ToJson(x)).ToArray()),
            offset + limit < all.Count, offset + limit < all.Count ? offset + limit : 0, all.Count);
    }
    public Task SaveAsync(string target, IReadOnlyList<BreezaryRow> rows, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailNextSave) { FailNextSave = false; throw new ApiException(503, "simulated database unavailable"); }
        if (!targets.TryGetValue(target, out var existing)) targets[target] = existing = new(StringComparer.Ordinal);
        foreach (var row in rows)
            if ((existing.TryGetValue(row.RecordId, out var old) ? old.Version : 0) != row.Version - 1)
                throw new ApiException(409, "stale version");
        foreach (var row in rows) existing[row.RecordId] = Clone(row);
        return Task.CompletedTask;
    }
    private static BreezaryRow Clone(BreezaryRow row) => row with
    {
        Remote = (JsonObject)row.Remote.DeepClone(), Pending = (JsonObject)row.Pending.DeepClone(), Baseline = (JsonObject)row.Baseline.DeepClone()
    };
    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; }
    }
}
