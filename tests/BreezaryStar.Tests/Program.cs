using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ScannerProject.BreezaryStar.WPF.Services;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
}

foreach (var (sse, shape) in new[] { (false, "success"), (true, "success"), (false, "values") })
{
    using var handler = new DocsHandler(sse, shape);
    using var http = new HttpClient(handler);
    var data = await new TencentDocsReader(http).ReadAsync("test-token", "file", "sheet", null, default);
    Check(data.Rows.Count == 2, "Read all record pages");
    Check(data.Rows[0]["工单号"].ToString() == "WO-001", "Rich text conversion");
    Check(data.Rows[0]["渠道"].ToString() == "UPS", "Option label conversion");
    Check(data.Rows[1]["运单号"].ToString() == "TRACK-002", "Dynamic columns across pages");
    Check(data.Rows[1]["记录 ID"].ToString() == "r2", "Preserve record identity");
    Check(handler.RecordCalls == 2 && handler.Initialized, "Initialize and paginate");
}

foreach (var mode in new[] { "vip", "token", "missing-sheet", "bad-page", "tool-error", "missing-records", "empty", "http-auth", "schema", "cancel" })
{
    using var handler = new DocsHandler(false, mode);
    using var http = new HttpClient(handler);
    try
    {
        var result = await new TencentDocsReader(http).ReadAsync("test-token", "file", "sheet", null, default);
        Check(mode == "empty" && result.Rows.Count == 0, "Only valid empty tables succeed");
    }
    catch (OperationCanceledException) when (mode == "cancel") { checks++; }
    catch (InvalidOperationException ex)
    {
        var expected = mode switch
        {
            "vip" => "VIP", "token" => "Token", "missing-sheet" => "未找到",
            "bad-page" => "分页", "tool-error" => "工具调用失败", "missing-records" => "records",
            "http-auth" => "拒绝访问", "schema" => "额外参数", _ => "UNEXPECTED"
        };
        Check(ex.Message.Contains(expected), $"Surface actionable {mode} error: {ex.Message}");
    }
}

using (var handler = new DocsHandler(false))
using (var http = new HttpClient(handler))
{
    try
    {
        await new TencentDocsReader(http).ReadAsync("", "file", "sheet", null, default);
        throw new Exception("Missing token should fail");
    }
    catch (InvalidOperationException) { Check(handler.Requests == 0, "Missing token never sends a request"); }
}
foreach (var mode in new[] { "success", "offline", "unauthorized", "not-deployed", "missing-values", "duplicate", "invalid-page" })
{
    using var handler = new ScannerHandler(mode);
    using var http = new HttpClient(handler);
    try
    {
        var data = await new ScannerServerReader(http).ReadAsync(new Uri("https://scanner.example"), "admin", "password-test", null, default);
        Check(mode is "success" or "offline" && data.Rows.Count == 2 && data.Rows[0]["工单号"].ToString() == "WO-001"
            && data.Rows[1]["运单号"].ToString() == "TRACK-002", "Server login and paginated reading");
    }
    catch (InvalidOperationException ex)
    {
        var expected = mode switch
        {
            "unauthorized" => "密码错误", "unconfigured" => "尚未配置", "disconnected" => "尚未完成",
            "not-deployed" => "部署", "missing-values" => "values", "duplicate" => "重复", "invalid-page" => "分页", _ => "UNEXPECTED"
        };
        Check(ex.Message.Contains(expected), "Actionable server-mode error " + mode);
    }
    Check(mode == "unauthorized" || handler.LoggedOut, "Temporary Scanner session closed after " + mode);
}
foreach (var value in new[] { "http://example.com", "https://name:secret@example.com", "https://example.com/?key=x", "https://example.com/app/" })
{
    try { ScannerServerReader.ServerUri(value); throw new Exception("Unsafe server URL accepted"); }
    catch (InvalidOperationException) { checks++; }
}
Check(ScannerServerReader.ServerUri("http://127.0.0.1:9000").IsLoopback, "Allow local development server");
Console.WriteLine($"Passed {checks} checks (in-memory HTTP; no live Tencent credentials used).");

sealed class DocsHandler(bool sse, string mode = "success") : HttpMessageHandler
{
    public int RecordCalls { get; private set; }
    public int Requests { get; private set; }
    public bool Initialized { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests++;
        if (request.RequestUri?.ToString() != TencentDocsReader.Endpoint || request.Method != HttpMethod.Post)
            throw new Exception("Unexpected endpoint or method");
        if (request.Headers.GetValues("Authorization").Single() != "test-token")
            throw new Exception("Missing Authorization token");
        if (mode == "cancel") throw new OperationCanceledException();
        if (mode == "http-auth") return new(HttpStatusCode.Unauthorized);
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
        var method = body["method"]!.ToString();
        if (method != "initialize" && (request.Headers.GetValues("Mcp-Session-Id").Single() != "session" ||
            request.Headers.GetValues("MCP-Protocol-Version").Single() != "2025-03-26"))
            throw new Exception("Session headers not preserved");
        if (method == "notifications/initialized")
        {
            Initialized = true;
            if (body["id"] is not null) throw new Exception("Notification must not have an id");
            return new(HttpStatusCode.Accepted);
        }

        JsonNode result;
        switch (method)
        {
            case "initialize":
                result = JsonNode.Parse("""{"protocolVersion":"2025-03-26","capabilities":{"tools":{}},"serverInfo":{"name":"test","version":"1"}}""")!;
                break;
            case "tools/list":
                result = JsonNode.Parse("""
                    {"tools":[
                      {"name":"smartsheet.list_tables","inputSchema":{"properties":{"file_id":{},"offset":{},"limit":{}},"required":["file_id"]}},
                      {"name":"smartsheet.list_records","inputSchema":{"properties":{"file_id":{},"sheet_id":{},"offset":{},"limit":{}},"required":["file_id","sheet_id"]}}
                    ]}
                    """)!;
                if (mode == "schema") ((JsonArray)result["tools"]![0]!["inputSchema"]!["required"]!).Add("new_required_argument");
                break;
            case "tools/call":
                if (!Initialized) throw new Exception("Tools called before initialization completed");
                var name = body["params"]!["name"]!.ToString();
                var arguments = body["params"]!["arguments"]!;
                if (arguments["file_id"]!.ToString() != "file") throw new Exception("Wrong file");
                JsonNode data;
                if (name == "smartsheet.list_tables")
                {
                    if (arguments["sheet_id"] is not null) throw new Exception("Unsupported argument sent");
                    data = JsonNode.Parse(mode == "missing-sheet" ? """{"sheets":[],"has_more":false}""" :
                        """{"sheets":[{"sheet_id":"sheet","title":"收货记录表"}],"has_more":false}""")!;
                }
                else if (name == "smartsheet.list_records")
                {
                    if (arguments["sheet_id"]!.ToString() != "sheet" || arguments["limit"]!.GetValue<int>() != 100)
                        throw new Exception("Wrong sheet or limit");
                    var expectedOffset = RecordCalls++;
                    if (arguments["offset"]!.GetValue<int>() != expectedOffset) throw new Exception("Incorrect page offset");
                    data = JsonNode.Parse(expectedOffset == 0 ?
                        """{"records":[{"record_id":"r1","field_values":{"工单号":[{"type":"text","text":"WO-001"}],"渠道":[{"label":"UPS"}]}}],"has_more":true,"next":1}""" :
                        """{"records":[{"record_id":"r2","field_values":{"工单号":"WO-002","运单号":"TRACK-002"}}],"has_more":false}""")!;
                    if (mode == "values")
                    {
                        foreach (var row in (JsonArray)data["records"]!)
                        {
                            row!["recordID"] = row["record_id"]!.DeepClone();
                            row["values"] = row["field_values"]!.DeepClone();
                            ((JsonObject)row).Remove("record_id");
                            ((JsonObject)row).Remove("field_values");
                        }
                    }
                    if (mode == "bad-page") data["next"] = 0;
                    if (mode == "empty") data = JsonNode.Parse("""{"records":[],"has_more":false}""")!;
                    if (mode == "missing-records") data = new JsonObject();
                }
                else throw new Exception("Client attempted a non-read tool");
                if (mode == "vip") data = JsonNode.Parse("""{"ret":400007,"msg":"denied"}""")!;
                if (mode == "token") data = JsonNode.Parse("""{"ret":400006,"msg":"denied"}""")!;
                result = sse
                    ? new JsonObject { ["structuredContent"] = new JsonObject { ["ret"] = 0, ["data"] = data } }
                    : new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = data.ToJsonString() }) };
                if (mode == "tool-error") result["isError"] = true;
                break;
            default: throw new Exception("Unexpected RPC method");
        }
        var envelope = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = body["id"]!.DeepClone(), ["result"] = result };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("Mcp-Session-Id", "session");
        response.Content = sse
            ? new StringContent("event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\n\nevent: message\ndata: " + envelope.ToJsonString() + "\n\n", Encoding.UTF8, "text/event-stream")
            : new StringContent(envelope.ToJsonString(), Encoding.UTF8, "application/json");
        return response;
    }
}

sealed class ScannerHandler(string mode) : HttpMessageHandler
{
    public bool LoggedOut;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri!.Host != "scanner.example") throw new Exception("WPF must only call Scanner.Web");
        var path = request.RequestUri.AbsolutePath;
        if (path == "/api/account/login")
        {
            var login = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
            if (request.Method != HttpMethod.Post || login["username"]!.ToString() != "admin" || login["password"]!.ToString() != "password-test")
                throw new Exception("Wrong Scanner login request");
            return mode == "unauthorized" ? Reply("""{"message":"用户名或密码错误"}""", HttpStatusCode.Unauthorized)
                : Reply("""{"token":"scanner-session","permissions":["system.configure"]}""");
        }
        if (request.Headers.Authorization?.ToString() != "Bearer scanner-session") throw new Exception("Missing Scanner session");
        if (path == "/api/account/logout") { LoggedOut = true; return Reply("{}"); }
        if (path == "/api/tencent-docs/status")
        {
            if (mode == "offline") throw new Exception("Local reads must not require Tencent status");
            if (mode == "not-deployed") return Reply("{}", HttpStatusCode.NotFound);
            return Reply(mode == "unconfigured" ? """{"configured":false}""" : mode == "disconnected"
                ? """{"configured":true,"connected":false}""" : """{"configured":true,"connected":true}""");
        }
        if (path != "/api/tencent-docs/records" || request.Method != HttpMethod.Get) throw new Exception("Unexpected WPF request");
        if (mode == "not-deployed") return Reply("{}", HttpStatusCode.NotFound);
        if (mode == "missing-values") return Reply("""{"records":[{"recordID":"r1"}],"hasMore":false}""");
        if (mode == "invalid-page") return Reply("""{"records":[{"recordID":"r1","values":{}}],"hasMore":true,"next":0}""");
        var secondPage = request.RequestUri.Query.Contains("offset=1");
        if (mode == "duplicate" && secondPage) return Reply("""{"records":[{"recordID":"r1","values":{}}],"hasMore":false}""");
        return Reply(secondPage
            ? """{"records":[{"recordID":"r2","values":{"运单号":"TRACK-002"}}],"hasMore":false,"next":0}"""
            : """{"records":[{"recordID":"r1","values":{"工单号":[{"text":"WO-001"}]}}],"hasMore":true,"next":1}""");
    }
    private static HttpResponseMessage Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}
