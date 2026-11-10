using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScannerProject.BreezaryStar.WPF.Services;

/// <summary>Read-only client for the official Tencent Docs Streamable HTTP MCP service.</summary>
public sealed class TencentDocsReader(HttpClient http)
{
    public const string Endpoint = "https://docs.qq.com/openapi/mcp";
    private string? sessionId;
    private string protocolVersion = "2025-03-26";
    private int requestId;

    public async Task<DataTable> ReadAsync(string token, string fileId, string sheetId,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("请先输入腾讯文档个人 Token。");
        if (string.IsNullOrWhiteSpace(fileId) || string.IsNullOrWhiteSpace(sheetId))
            throw new InvalidOperationException("请填写文档 ID 和工作表 ID。");

        progress?.Report("正在连接腾讯文档…");
        var initialized = await RpcAsync("initialize", new
        {
            protocolVersion,
            capabilities = new { },
            clientInfo = new { name = "BreezaryStar", version = "0.1.0" }
        }, token, cancellationToken);
        protocolVersion = initialized["protocolVersion"]?.GetValue<string>() ?? protocolVersion;
        await RpcAsync("notifications/initialized", new { }, token, cancellationToken, notification: true);

        // Inspect the current official tool schema instead of assuming an outdated API definition.
        var definitions = new List<JsonNode>();
        string? cursor = null;
        var cursors = new HashSet<string>();
        do
        {
            var page = await RpcAsync("tools/list", cursor is null ? new { } : (object)new { cursor }, token, cancellationToken);
            if (page["tools"] is not JsonArray tools)
                throw new InvalidOperationException("腾讯文档未返回工具列表。");
            definitions.AddRange(tools.Select(t => t!).Where(t => t is not null));
            cursor = page["nextCursor"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(cursor) && !cursors.Add(cursor))
                throw new InvalidOperationException("工具列表分页游标重复，已停止读取。");
        } while (!string.IsNullOrEmpty(cursor));

        var tablesTool = FindTool(definitions, "smartsheet.list_tables");
        var recordsTool = FindTool(definitions, "smartsheet.list_records");
        progress?.Report("正在验证工作表…");
        var tables = await ReadPagesAsync(tablesTool, fileId, sheetId, "sheets", token, cancellationToken);
        if (!tables.Any(t => t["sheet_id"]?.ToString() == sheetId))
            throw new InvalidOperationException("未找到该工作表。请检查文档 ID、工作表 ID 和当前 Token 的访问权限。");

        var result = new DataTable();
        result.Columns.Add("记录 ID");
        var seenIds = new HashSet<string>();
        var offset = 0;
        while (true)
        {
            var arguments = Arguments(recordsTool, fileId, sheetId, offset);
            var data = await CallToolAsync(recordsTool, arguments, token, cancellationToken);
            if (data["records"] is not JsonArray records)
                throw new InvalidOperationException("接口未返回 records 数组，无法确认读取成功。");
            foreach (var record in records)
            {
                var id = (record?["record_id"] ?? record?["recordID"])?.ToString();
                if (string.IsNullOrEmpty(id) || !seenIds.Add(id))
                    throw new InvalidOperationException("返回的记录 ID 缺失或重复，已停止读取，请重新拉取。");
                if ((record?["field_values"] ?? record?["values"]) is not JsonObject values)
                    throw new InvalidOperationException("接口未返回记录字段值（field_values / values）。");
                foreach (var pair in values)
                    if (!result.Columns.Contains(pair.Key)) result.Columns.Add(pair.Key);
                var row = result.NewRow();
                row["记录 ID"] = id;
                foreach (var pair in values) row[pair.Key] = CellText(pair.Value);
                result.Rows.Add(row);
            }
            progress?.Report($"已读取 {result.Rows.Count} 条记录…");
            if (!HasMore(data)) break;
            offset = NextOffset(data, offset, records.Count);
            if (result.Rows.Count >= 10000)
                throw new InvalidOperationException("本次测试超过 10000 条记录，请缩小表格范围后重试。");
        }
        return result;
    }

    private async Task<List<JsonNode>> ReadPagesAsync(JsonNode tool, string fileId, string sheetId,
        string arrayName, string token, CancellationToken cancellationToken)
    {
        var items = new List<JsonNode>();
        var offset = 0;
        while (true)
        {
            var data = await CallToolAsync(tool, Arguments(tool, fileId, sheetId, offset), token, cancellationToken);
            // list_tables uses "tables" in some server versions and "sheets" in others.
            var entries = data[arrayName] as JsonArray ?? data["tables"] as JsonArray;
            if (entries is null) throw new InvalidOperationException("接口未返回工作表列表，请核对服务端工具定义。");
            items.AddRange(entries.Where(x => x is not null).Select(x => x!));
            if (!HasMore(data)) return items;
            offset = NextOffset(data, offset, entries.Count);
            if (items.Count >= 10000) throw new InvalidOperationException("工作表列表超出测试读取上限。");
        }
    }

    private static JsonNode FindTool(List<JsonNode> tools, string name) =>
        tools.FirstOrDefault(t => t["name"]?.ToString() == name)
        ?? throw new InvalidOperationException($"当前腾讯文档服务未开放 {name} 工具。");

    private static JsonObject Arguments(JsonNode tool, string fileId, string sheetId, int offset)
    {
        var properties = tool["inputSchema"]?["properties"] as JsonObject
            ?? throw new InvalidOperationException("工具缺少参数定义。");
        var candidates = new JsonObject
        {
            ["file_id"] = fileId, ["sheet_id"] = sheetId, ["offset"] = offset, ["limit"] = 100
        };
        var args = new JsonObject();
        foreach (var pair in candidates)
            if (properties.ContainsKey(pair.Key)) args[pair.Key] = pair.Value?.DeepClone();
        if (tool["inputSchema"]?["required"] is JsonArray required)
            foreach (var key in required)
                if (key is not null && !args.ContainsKey(key.ToString()))
                    throw new InvalidOperationException($"当前工具要求额外参数 {key}，请按最新官方定义更新接入。");
        return args;
    }

    private async Task<JsonNode> CallToolAsync(JsonNode tool, JsonObject args, string token, CancellationToken ct)
    {
        var result = await RpcAsync("tools/call", new { name = tool["name"]!.ToString(), arguments = args }, token, ct);
        if (result["isError"]?.GetValue<bool>() == true)
            throw new InvalidOperationException("腾讯文档工具调用失败：" + ErrorText(result));
        JsonNode? payload = result["structuredContent"];
        if (payload is null && result["content"] is JsonArray content)
        {
            foreach (var part in content)
            {
                if (part?["type"]?.ToString() != "text") continue;
                try { payload = JsonNode.Parse(part["text"]!.ToString()); }
                catch (JsonException) { continue; }
                if (payload is JsonObject) break;
            }
        }
        if (payload is not JsonObject) throw new InvalidOperationException("腾讯文档未返回结构化数据：" + ErrorText(result));
        CheckError(payload);
        var data = payload["data"] ?? payload;
        CheckError(data);
        return data;
    }

    private async Task<JsonNode> RpcAsync(string method, object parameters, string token, CancellationToken ct,
        bool notification = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.TryAddWithoutValidation("Authorization", token);
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
        if (method != "initialize") request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", protocolVersion);
        if (sessionId is not null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0", ["method"] = method,
            ["params"] = JsonSerializer.SerializeToNode(parameters)
        };
        var id = ++requestId;
        if (!notification) envelope["id"] = id;
        request.Content = JsonContent.Create(envelope);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("腾讯文档拒绝访问，请检查 Token、文档访问权限及账号 VIP 权益。");
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("腾讯文档请求频率受限，请稍后重试。");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"腾讯文档 HTTP 请求失败（{(int)response.StatusCode}）。");
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var ids)) sessionId = ids.First();
        if (notification) return new JsonObject();
        JsonNode? message;
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (true)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null || line.Length == 0)
                {
                    if (lines.Count > 0)
                    {
                        message = JsonNode.Parse(string.Join("\n", lines));
                        lines.Clear();
                        if (message?["id"]?.ToString() == id.ToString()) return RpcResult(message);
                    }
                    if (line is null) break;
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal)) lines.Add(line[5..].TrimStart(' '));
            }
            throw new InvalidOperationException("腾讯文档连接提前结束，未收到请求结果。");
        }
        message = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        // Some gateway errors are returned outside the JSON-RPC envelope.
        if (message is not null) CheckError(message);
        if (message?["id"]?.ToString() != id.ToString())
            throw new InvalidOperationException("腾讯文档返回的请求 ID 不匹配。");
        return RpcResult(message);
    }

    private static JsonNode RpcResult(JsonNode message)
    {
        if (message["error"] is not null)
            throw new InvalidOperationException("腾讯文档请求失败：" + message["error"]);
        return message["result"] ?? throw new InvalidOperationException("腾讯文档响应缺少 result。");
    }

    private static void CheckError(JsonNode node)
    {
        var code = node["ret"] ?? node["code"];
        if (code is not null && code.ToString() is not ("0" or ""))
        {
            var hint = code.ToString() switch
            {
                "400006" => "Token 无效或已过期，请重新获取。",
                "400007" => "账号 VIP 权益不足，请在腾讯文档确认所需权益。",
                _ => (node["msg"] ?? node["message"])?.ToString() ?? "请检查访问权限和参数。"
            };
            throw new InvalidOperationException($"腾讯文档错误 {code}：{hint}");
        }
        if (node["error"] is JsonValue error && !string.IsNullOrWhiteSpace(error.ToString()))
            throw new InvalidOperationException("腾讯文档错误：" + error);
    }

    private static string ErrorText(JsonNode result) => result["content"]?.ToJsonString() ?? result.ToJsonString();
    private static bool HasMore(JsonNode data) => (data["has_more"] ?? data["hasMore"] ?? data["has_next"])?.GetValue<bool>() == true;
    private static int NextOffset(JsonNode data, int offset, int count)
    {
        var next = data["next"]?.GetValue<int>() ?? offset + count;
        if (count == 0 || next <= offset) throw new InvalidOperationException("接口返回了无效的分页位置，已停止读取。");
        return next;
    }

    public static string CellText(JsonNode? value)
    {
        if (value is null) return "";
        if (value is JsonArray array) return string.Join(" / ", array.Select(CellText));
        if (value is JsonObject obj)
        {
            foreach (var key in new[] { "text", "label", "name", "title", "value" })
                if (obj[key] is not null) return CellText(obj[key]);
            return obj.ToJsonString();
        }
        return value.ToString();
    }
}
