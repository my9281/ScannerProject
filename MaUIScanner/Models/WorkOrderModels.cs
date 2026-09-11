using System.Text.Json.Serialization;

namespace MaUIScanner.Models;

public sealed class WorkOrderRemarkResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    [JsonPropertyName("urgent_count")]
    public int UrgentCount { get; set; }
    [JsonPropertyName("returned_count")]
    public int ReturnedCount { get; set; }
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
    [JsonPropertyName("work_orders")]
    public List<WorkOrderRemark> WorkOrders { get; set; } = new();
}

public sealed class WorkOrderRemark
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("sn")]
    public string? Sn { get; set; }
    [JsonPropertyName("tracking_number")]
    public string? TrackingNumber { get; set; }
    [JsonPropertyName("remark")]
    public string? Remark { get; set; }
    [JsonPropertyName("remark_timestamp")]
    public long RemarkTimestamp { get; set; }
    [JsonPropertyName("is_urgent")]
    public bool IsUrgent { get; set; }
    [JsonIgnore]
    public bool IsRepair { get; set; }
    [JsonIgnore]
    public bool IsOidRule { get; set; }
}
