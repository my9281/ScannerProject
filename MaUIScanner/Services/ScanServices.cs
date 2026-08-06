using System.Text.RegularExpressions;

namespace MaUIScanner.Services;

public sealed class OidScanResult
{
    public static OidScanResult NotOid { get; } = new(false, 0);
    public OidScanResult(bool isOid, int occurrence)
    {
        IsOid = isOid;
        Occurrence = occurrence;
    }
    public bool IsOid { get; }
    public int Occurrence { get; }
    public bool ShouldPrint => IsOid && Occurrence >= 2;
}

public sealed class ScanResult
{
    public ScanResult(string code, bool wasRecorded, OidScanResult oid)
    {
        Code = code;
        WasRecorded = wasRecorded;
        Oid = oid;
    }
    public string Code { get; }
    public bool WasRecorded { get; }
    public OidScanResult Oid { get; }
}

public sealed class OidService
{
    private static readonly Regex LongNumeric = new("^\\d{34}$", RegexOptions.Compiled);
    private static readonly Regex FedEx = new("^\\d{12}$", RegexOptions.Compiled);
    private static readonly Regex Amazon = new("^1Z[A-Z0-9]{16}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
    public OidScanResult Inspect(string code)
    {
        string value = code.Trim();
        if (!LongNumeric.IsMatch(value) && !FedEx.IsMatch(value) && !Amazon.IsMatch(value)) return OidScanResult.NotOid;
        _counts.TryGetValue(value, out int count);
        _counts[value] = ++count;
        return new OidScanResult(true, count);
    }
}

public sealed class ScanService
{
    private readonly HashSet<string> _recorded = new(StringComparer.OrdinalIgnoreCase);
    private readonly OidService _oid;
    private readonly ScanLogService _log;
    public ScanService(OidService oid, ScanLogService log)
    {
        _oid = oid;
        _log = log;
    }
    public int Count => _recorded.Count;
    public string LogPath => _log.FilePath;
    public async Task<ScanResult> RecordAsync(string input)
    {
        string code = input.Trim();
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("扫描内容不能为空。");
        bool wasRecorded = _recorded.Add(code);
        if (wasRecorded) await _log.AppendAsync(code);
        return new ScanResult(code, wasRecorded, _oid.Inspect(code));
    }
}

public sealed class MeterModelService
{
    private static readonly string[] Models = { "AC200PL", "AC200L", "AC200P", "AC200M", "AC180P", "AC180T", "EL100V2", "EL200V2", "B300K2", "SP100L", "AC180", "AC240", "AC300", "AC500", "EL30V2", "EL300", "EL400", "B300K", "B300S", "B500K", "PV350", "PV200", "AP300", "AC50B", "AC2A", "AC2P", "EB3A", "AC60", "AC70", "EL10", "B230", "B300", "PS54", "EB55", "EB70", "PINA" };
    public string Find(string code) => Models.FirstOrDefault(model => code.Contains(model, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
}

