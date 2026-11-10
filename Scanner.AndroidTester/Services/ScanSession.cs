namespace Scanner.AndroidTester.Services;

public sealed record ScanResult(string Value, bool SameAsLast, bool InPreScanList);
public sealed class ScanSession
{
    private readonly List<string> _preScans = new();
    public IReadOnlyList<string> PreScans => _preScans.AsReadOnly();
    public string? LastScan { get; private set; }
    public bool AddPreScan(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        if (_preScans.Contains(value, StringComparer.Ordinal)) return false;
        _preScans.Add(value);
        return true;
    }
    public ScanResult? Detect(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        var result = new ScanResult(value, string.Equals(value, LastScan, StringComparison.Ordinal), _preScans.Contains(value, StringComparer.Ordinal));
        LastScan = value;
        return result;
    }
}
