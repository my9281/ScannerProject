using Scanner.Server.Model;
using System.Globalization;
using System.Net.Http.Json;
namespace Scanner.AndroidTester.Services;
public sealed class SkuSnSession
{
    private readonly List<SkuSnItem> _items = new();
    public IReadOnlyList<SkuSnItem> Items => _items.AsReadOnly();
    public string? PendingSku { get; private set; }
    public Guid BatchId { get; private set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.Now;
    public string BatchNumber => CreatedAt.ToString("yyyyMMddmmss", CultureInfo.InvariantCulture);
    public bool IsUploading { get; private set; }
    private SkuSnBatch? _snapshot;
    private readonly string? _statePath;
    public SkuSnSession(string? statePath = null)
    {
        _statePath = statePath;
        if (statePath is not null && File.Exists(statePath))
        {
            var state = System.Text.Json.JsonSerializer.Deserialize<SavedState>(File.ReadAllText(statePath))
                ?? throw new InvalidOperationException("批次缓存无效。");
            BatchId = state.Batch.BatchId; CreatedAt = state.Batch.CreatedAt;
            _items.AddRange(state.Batch.Items); PendingSku = state.PendingSku;
            if (state.Sealed) _snapshot = state.Batch;
        }
    }
    private void Save()
    {
        if (_statePath is null) return;
        var state = new SavedState(new(BatchId, BatchNumber, CreatedAt, _items.ToArray()), PendingSku, _snapshot is not null);
        File.WriteAllText(_statePath + ".tmp", System.Text.Json.JsonSerializer.Serialize(state));
        File.Move(_statePath + ".tmp", _statePath, true);
    }
    private sealed record SavedState(SkuSnBatch Batch, string? PendingSku, bool Sealed);
    public void Scan(string value)
    {
        if (IsUploading || _snapshot is not null) throw new ArgumentException("批次已封存，请重试上传。");
        value = value.Trim();
        int max = PendingSku is null ? 150 : 100;
        if (value.Length < 1 || value.Length > max || value.Any(char.IsControl)) throw new ArgumentException("扫码内容无效。");
        if (_items.Count >= 5000) throw new ArgumentException("每批最多 5000 条，请上传。");
        if (PendingSku is null) { if (_items.Count == 0) CreatedAt = DateTimeOffset.Now; PendingSku = value; Save(); return; }
        if (_items.Any(x => x.Sn == value)) throw new ArgumentException("本批 SN 重复。");
        _items.Add(new(PendingSku, value)); PendingSku = null; Save();
    }
    public void ResetPair() { if (!IsUploading && _snapshot is null) { PendingSku = null; Save(); } }
    public async Task UploadAsync(HttpClient client, AppSettings settings)
    {
        if (IsUploading || PendingSku is not null || _items.Count == 0) throw new InvalidOperationException("请完成 SKU/SN 配对后上传。");
        IsUploading = true;
        try
        {
            _snapshot ??= new(BatchId, BatchNumber, CreatedAt, _items.ToArray());
            Save();
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/sku-sn-batches") { Content = JsonContent.Create(_snapshot) };
            request.Headers.Add("X-Upload-Key", settings.UploadApiKey);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"上传失败 HTTP {(int)response.StatusCode}，批次已保留。");
            var ack = await response.Content.ReadFromJsonAsync<Acknowledgement>();
            if (ack?.BatchId != BatchId || ack.ItemCount != _items.Count) throw new InvalidOperationException("批次确认不完整，请重试。");
            _items.Clear(); _snapshot = null; BatchId = Guid.NewGuid(); CreatedAt = DateTimeOffset.Now; Save();
        }
        finally { IsUploading = false; }
    }
    private sealed record Acknowledgement(Guid BatchId, int ItemCount);
}
