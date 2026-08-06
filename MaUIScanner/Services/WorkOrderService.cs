using MaUIScanner.Models;

namespace MaUIScanner.Services;

public sealed class WorkOrderSearchService
{
    private List<WorkOrderRemark> _items = new();
    public int Count => _items.Count;
    public void Replace(IEnumerable<WorkOrderRemark>? items) => _items = items?.Where(item => item != null).ToList() ?? new();
    public void Merge(IEnumerable<WorkOrderRemark> items)
    {
        foreach (WorkOrderRemark item in items.Reverse())
        {
            _items.RemoveAll(existing => Equal(existing.Sn, item.Sn) || Equal(existing.TrackingNumber, item.TrackingNumber));
            _items.Insert(0, item);
        }
    }
    public WorkOrderRemark? Find(string code) => _items.FirstOrDefault(item => Equal(item.Sn, code) || Equal(item.TrackingNumber, code));
    private static bool Equal(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
