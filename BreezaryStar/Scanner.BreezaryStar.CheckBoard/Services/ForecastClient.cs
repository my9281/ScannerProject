using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Scanner.BreezaryStar.CheckBoard.Models;

namespace Scanner.BreezaryStar.CheckBoard.Services;

public sealed class ForecastClient(HttpClient client)
{
    public async Task<IReadOnlyList<ForecastRow>> DownloadAsync(Uri server, string token, CancellationToken ct = default)
    {
        var all = new List<ForecastRow>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var offset = 0;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server, $"api/tencent-docs/records?offset={offset}&limit=100"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new InvalidOperationException("Scanner 登录已失效，请返回工作台重新登录。");
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new InvalidOperationException("查看预报接口目前要求 Scanner 管理员权限。");
            if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("服务器尚未部署预报接口，请更新 Scanner.Web。");
            JsonNode? page;
            try { page = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)); }
            catch (JsonException) { throw new InvalidOperationException("服务器返回了无效响应，请检查服务地址和部署版本。"); }
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(page?["message"]?.ToString() ?? $"预报查询失败（HTTP {(int)response.StatusCode}）。");
            if (page?["records"] is not JsonArray records || page["hasMore"] is null)
                throw new InvalidOperationException("服务器返回的预报分页格式无效。");
            foreach (var item in records)
            {
                var row = ForecastRow.Parse(item ?? throw new InvalidOperationException("预报记录为空。"));
                if (!ids.Add(row.RecordId)) throw new InvalidOperationException("分页返回了重复记录，请刷新后重试。");
                all.Add(row);
            }
            if (!page["hasMore"]!.GetValue<bool>()) return all;
            var next = page["next"]?.GetValue<int>() ?? 0;
            if (records.Count == 0 || next <= offset) throw new InvalidOperationException("服务器返回了无效分页位置。");
            if (all.Count >= 100000) throw new InvalidOperationException("预报记录超过 100000 条，请联系管理员缩小数据范围。");
            offset = next;
        }
    }
}
