using Scanner.Models.MauiContracts;

namespace Scanner.MaUI.Services;

public sealed class WorkOrderSearchService
{
    private List<WorkOrderRemark> _online = new(), _imported = new();
    private WorkOrderRemark? _activeOidRule;
    public int Count => _online.Count + _imported.Count;
    public void Replace(IEnumerable<WorkOrderRemark>? items) => _online = items?.Where(x => x is not null).ToList() ?? new();
    public void Merge(IEnumerable<WorkOrderRemark> items) { _imported = items.Where(x => x is not null).ToList(); _activeOidRule = null; }
    public WorkOrderMatch? Resolve(string code, bool isOid)
    {
        string normalized = Normalize(code); if (normalized.Length == 0) return null;
        if (isOid)
        {
            _activeOidRule = _imported.Where(x => x.IsOidRule && Contains(normalized, x.TrackingNumber)).OrderByDescending(x => Normalize(x.TrackingNumber).Length).FirstOrDefault();
            if (_activeOidRule is not null) return new(_activeOidRule, false);
            var exactOid = FindExact(normalized); return exactOid is not null && !exactOid.IsOidRule ? new(exactOid, true) : null;
        }
        if (_activeOidRule is not null) return new(_activeOidRule, false);
        var exact = FindExact(normalized); return exact is null ? null : new(exact, true);
    }
    private WorkOrderRemark? FindExact(string code) => _imported.FirstOrDefault(x => !x.IsOidRule && Equal(x.Sn, code)) ?? _online.FirstOrDefault(x => Equal(x.Sn, code) || Equal(x.TrackingNumber, code));
    private static bool Contains(string oid, string? tracking) { string value = Normalize(tracking); return value.Length > 0 && oid.Contains(value, StringComparison.OrdinalIgnoreCase); }
    private static bool Equal(string? left, string right) => Normalize(left).Equals(right, StringComparison.OrdinalIgnoreCase);
    public static string Normalize(string? value) => string.Concat((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '\uFEFF' && c != '\u200B'));
}

public sealed record WorkOrderMatch(WorkOrderRemark WorkOrder, bool UseMatchedSn)
{
    public string GetPrintCode(string scannedCode) => UseMatchedSn && !string.IsNullOrWhiteSpace(WorkOrder.Sn) ? WorkOrderSearchService.Normalize(WorkOrder.Sn) : WorkOrderSearchService.Normalize(scannedCode);
}
