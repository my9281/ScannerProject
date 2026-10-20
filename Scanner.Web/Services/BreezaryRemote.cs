using System.Text.Json.Nodes;

namespace Scanner.Web.Services;

public sealed class BreezaryRemote(TencentDocsService docs) : IBreezaryRemote
{
    public async Task<IReadOnlyList<BreezaryRow>> ReadAllAsync(CancellationToken ct)
    {
        var result = new List<BreezaryRow>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var offset = 0;
        while (true)
        {
            var page = await docs.GetRecordsAsync(offset, 100, ct);
            foreach (var record in page.Records)
            {
                var id = record?["recordID"]?.ToString();
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id) || record?["values"] is not JsonObject values)
                    throw new ApiException(502, "腾讯记录 ID 或字段结构无效/重复，本次未入库。");
                result.Add(new() { RecordId = id, FileId = page.FileId, SheetId = page.SheetId, Remote = (JsonObject)values.DeepClone() });
            }
            if (!page.HasMore) return result;
            if (result.Count >= 100000) throw new ApiException(413, "单次同步超过 100000 条，请拆分数据后重试。");
            offset = page.Next;
        }
    }

    public Task<JsonObject> UpdateAsync(string recordId, JsonObject values, CancellationToken ct) => docs.UpdateRecordAsync(recordId, values, ct);
}
