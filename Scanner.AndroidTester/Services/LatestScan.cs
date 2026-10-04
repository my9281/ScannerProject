namespace Scanner.AndroidTester.Services;
public sealed class LatestScan
{
    public string? Value { get; private set; }
    public void Record(string? value) { if (!string.IsNullOrWhiteSpace(value)) Value = value.Trim(); }
}
