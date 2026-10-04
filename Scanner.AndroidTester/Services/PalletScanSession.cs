using Scanner.Server.Model;
namespace Scanner.AndroidTester.Services;

public sealed class PalletScanSession
{
    private readonly List<PalletScanItem> _pending = new();
    private readonly HashSet<string> _scanned = new(StringComparer.Ordinal);
    public IReadOnlyList<PalletScanItem> Pending => _pending.AsReadOnly();
    public int PalletNumber { get; set; } = 1;
    public string ApiKey { get; set; } = "";
    public bool IsUploading { get; private set; }
    public void Add(string value, DateTimeOffset time)
    {
        var sn = value.Trim();
        if (sn.Length is < 1 or > 100) throw new ArgumentException("请扫描 1–100 个字符的 SN。");
        if (PalletNumber is < 1 or > 10) throw new ArgumentException("托盘号必须为 1–10。");
        if (!_scanned.Add(sn)) throw new ArgumentException("重复 SN，本次运行已扫描过，未重复记录。");
        _pending.Add(new PalletScanItem(Guid.NewGuid(), sn, PalletNumber, time));
    }
    public async Task<int> UploadAsync(PalletScanApiService api)
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
                var accepted = batch.Select(x => x.ScanId).ToHashSet();
                _pending.RemoveAll(x => accepted.Contains(x.ScanId));
                count += batch.Length;
            }
            return count;
        }
        finally { IsUploading = false; }
    }
}
