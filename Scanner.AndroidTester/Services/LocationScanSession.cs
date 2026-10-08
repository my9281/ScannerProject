using Scanner.Server.Model;
namespace Scanner.AndroidTester.Services;

public sealed class LocationScanSession
{
    private readonly List<LocationScanItem> _pending = new();
    private readonly HashSet<string> _scanned = new(StringComparer.Ordinal);
    public IReadOnlyList<LocationScanItem> Pending => _pending.AsReadOnly();
    
    public string ApiKey { get; set; } = "";
    public bool IsUploading { get; private set; }
    public void Add(string value, DateTimeOffset time)
    {
        var sn = value.Trim();
        if (sn.Length is < 1 or > 100) throw new ArgumentException("请扫描 1–100 个字符的库位 ID。");
        if (sn.Any(char.IsControl)) throw new ArgumentException("库位 ID 不能包含控制字符。");
        if (!_scanned.Add(sn)) throw new ArgumentException("重复库位 ID，本次运行已扫描过，未重复记录。");
        _pending.Add(new LocationScanItem(sn, time));
    }
    public async Task<int> UploadAsync(LocationScanApiService api)
    {
        if (IsUploading) throw new InvalidOperationException("正在上传，请稍候。");
        if (_pending.Count == 0) throw new InvalidOperationException("没有待上传记录。");
        IsUploading = true;
        int count = 0;
        try
        {
            var snapshot = _pending.ToArray();
            foreach (var batch in snapshot.Chunk(1000))
            {
                await api.UploadAsync(batch, ApiKey);
                var accepted = batch.Select(x => x.LocationId).ToHashSet();
                _pending.RemoveAll(x => accepted.Contains(x.LocationId));
                count += batch.Length;
            }
            return count;
        }
        finally { IsUploading = false; }
    }
}
