using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Scanner.Web.Services;

public sealed record BreezaryRow
{
    public string RecordId { get; init; } = "";
    public string FileId { get; init; } = "";
    public string SheetId { get; init; } = "";
    public JsonObject Remote { get; init; } = new();
    public JsonObject Pending { get; init; } = new();
    public JsonObject Baseline { get; init; } = new();
    public string SyncStatus { get; init; } = "clean";
    public string LastError { get; init; } = "";
    public long Version { get; init; }
    public DateTime? LastPulledAt { get; init; }
    public DateTime? LastPushedAt { get; init; }
    public JsonObject Values => BreezaryMerge.Merge(Remote, Pending);
}

public sealed record BreezaryEditRequest(long Version, JsonObject Values, bool Rebase = false);
public sealed record BreezaryLocalPage(JsonArray Records, bool HasMore, int Next, long Total, string Source = "database");
public sealed record BreezarySyncResult(int Pulled, int Pushed, int Conflicts, int Failed);

public interface IBreezaryStore
{
    Task<IAsyncDisposable> AcquireAsync(string target, CancellationToken ct);
    Task<IReadOnlyList<BreezaryRow>> ReadAllAsync(string target, CancellationToken ct);
    Task<BreezaryLocalPage> ListAsync(string target, int offset, int limit, CancellationToken ct);
    Task SaveAsync(string target, IReadOnlyList<BreezaryRow> rows, CancellationToken ct);
}

public interface IBreezaryRemote
{
    Task<IReadOnlyList<BreezaryRow>> ReadAllAsync(CancellationToken ct);
    Task<JsonObject> UpdateAsync(string recordId, JsonObject values, CancellationToken ct);
}

public static class BreezaryMerge
{
    public static string Target(TencentDocsOptions options) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes((string.IsNullOrEmpty(options.FileId) ? options.EncodedId : options.FileId) + "\n" + options.SheetId))).ToLowerInvariant();

    public static JsonObject Merge(JsonObject remote, JsonObject pending)
    {
        var result = (JsonObject)remote.DeepClone();
        foreach (var pair in pending) result[pair.Key] = pair.Value?.DeepClone();
        return result;
    }

    public static BreezaryRow Edit(BreezaryRow row, BreezaryEditRequest request)
    {
        if (request.Version != row.Version) throw new ApiException(409, "记录已被更新，请刷新本地数据后再编辑。");
        if (request.Values is null || request.Values.Count == 0 || request.Values.ToJsonString().Length > 100000)
            throw new ApiException(400, "请提交需要修改的字段值，大小不能超过 100000 字符。");
        var patch = (JsonObject)row.Pending.DeepClone();
        var baseline = (JsonObject)row.Baseline.DeepClone();
        foreach (var pair in request.Values)
        {
            if (!row.Remote.ContainsKey(pair.Key)) throw new ApiException(400, "不能新增未知字段：" + pair.Key);
            if (row.Remote[pair.Key] is JsonArray oldOptions && oldOptions.Any(x => x is JsonObject o && o.ContainsKey("id") && o.ContainsKey("text")) && pair.Value is not null)
            {
                if (pair.Value is not JsonArray choices || choices.Any(x => x is not JsonObject o || string.IsNullOrEmpty(o["id"]?.ToString())))
                    throw new ApiException(400, pair.Key + " 是选项字段，请保留带正确选项 ID 的数组格式。");
                foreach (var choice in choices)
                {
                    var original = oldOptions.FirstOrDefault(x => x?["id"]?.ToString() == choice?["id"]?.ToString());
                    if (original is not null && choice?["text"] is not null && original["text"]?.ToString() != choice["text"]!.ToString())
                        throw new ApiException(400, pair.Key + " 不能只更改显示文字而沿用原选项 ID，请使用目标选项的正确 ID。");
                }
            }
            if (Equivalent(row.Remote[pair.Key], pair.Value))
            {
                patch.Remove(pair.Key); baseline.Remove(pair.Key);
            }
            else
            {
                if (request.Rebase || !baseline.ContainsKey(pair.Key)) baseline[pair.Key] = row.Remote[pair.Key]?.DeepClone();
                patch[pair.Key] = pair.Value?.DeepClone();
            }
        }
        return row with { Pending = patch, Baseline = baseline, Version = row.Version + 1,
            SyncStatus = patch.Count > 0 ? "pending" : "clean", LastError = "" };
    }

    public static bool Conflicts(BreezaryRow row, JsonObject current) => row.Pending.Any(pair =>
        !current.ContainsKey(pair.Key) || (!Equivalent(current[pair.Key], row.Baseline[pair.Key])
            && !Equivalent(current[pair.Key], pair.Value)));

    public static bool Equivalent(JsonNode? left, JsonNode? right) => JsonNode.DeepEquals(Normalize(left), Normalize(right));

    private static JsonNode? Normalize(JsonNode? node)
    {
        if (node is JsonArray array) return new JsonArray(array.Select(Normalize).ToArray());
        if (node is not JsonObject obj) return node?.DeepClone();
        // Option identity is its ID; server-controlled display text/color may change independently.
        if (obj.ContainsKey("id") && obj.ContainsKey("text")) return new JsonObject { ["id"] = obj["id"]?.DeepClone() };
        var copy = new JsonObject();
        foreach (var pair in obj)
        {
            if (pair.Key == "format" && pair.Value is JsonObject format && format.Count == 0) continue;
            copy[pair.Key] = Normalize(pair.Value);
        }
        return copy;
    }

    public static JsonObject ToJson(BreezaryRow row) => new()
    {
        ["recordID"] = row.RecordId, ["values"] = row.Values, ["version"] = row.Version,
        ["remoteValues"] = row.SyncStatus == "conflict" ? row.Remote.DeepClone() : null,
        ["syncStatus"] = row.SyncStatus, ["lastError"] = row.LastError,
        ["lastPulledAt"] = row.LastPulledAt?.ToString("O"), ["lastPushedAt"] = row.LastPushedAt?.ToString("O")
    };

    public static string Text(JsonNode? value)
    {
        if (value is null) return "";
        if (value is JsonArray array) return string.Join(" / ", array.Select(Text));
        if (value is JsonObject obj)
        {
            foreach (var key in new[] { "text", "label", "name", "title", "value" })
                if (obj.ContainsKey(key)) return Text(obj[key]);
            return obj.ToJsonString();
        }
        return value.ToString();
    }
}
