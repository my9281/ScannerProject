using System.Net;
using System.Text.Json;
using Scanner.Server.Model;
using WPF.Camara;

internal static class DesktopRepairChecks
{
    public static async Task RunAsync()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "repair-start-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "pending.json");
        var first = DateTimeOffset.UtcNow.AddMinutes(-1);
        RepairScan pending;
        try
        {
            using (var session = new RepairStartSession(path))
            {
                session.Update("", "SKU-FIRST", first);
                Check(!session.CanSubmit && session.StartedAt == first, "SKU-first starts clock but cannot submit");
                session.Update("0000123", "SKU-FIRST", first.AddSeconds(10));
                Check(session.CanSubmit && session.StartedAt == first, "second code preserves first scan time");
                session.Update("", "SKU-FIRST", first.AddSeconds(20));
                Check(!session.CanSubmit && session.StartedAt == first, "single clear preserves clock");
                session.Update("", "", first.AddSeconds(30));
                Check(session.StartedAt is null, "clear both resets clock");
                session.Update(new string('S', 51), "SKU", first);
                Check(!session.CanSubmit, "overlong scan cannot submit or silently truncate");
                session.Update("0000123", new string('K', 50), first);
                pending = session.PrepareUpload();
                Check(File.Exists(path) && pending.Sn == "0000123", "persist before sending and preserve zeroes");
                Check(session.PrepareUpload() == pending, "retry reuses request ID and time");
                try { session.Update("OTHER", "SKU", first); throw new Exception("Pending edit allowed"); }
                catch (InvalidOperationException) { }
            }
            using var restored = new RepairStartSession(path);
            Check(restored.Pending == pending && restored.StartedAt == first, "restart restores pending record");
            using var handler = new RepairHandler();
            using var client = new HttpClient(handler);
            var api = new RepairStartApi(client);
            var endpoint = new Uri("https://example.test/wms/");
            handler.Status = HttpStatusCode.Unauthorized;
            await MustFail(() => api.UploadAsync(endpoint, "test-key", pending));
            Check(File.Exists(path), "failure retains pending file");
            handler.Status = HttpStatusCode.OK;
            handler.WrongId = true;
            await MustFail(() => api.UploadAsync(endpoint, "test-key", pending));
            Check(restored.Pending == pending, "mismatched confirmation retains record");
            handler.WrongId = false;
            await api.UploadAsync(endpoint, "test-key", pending);
            restored.Confirm(pending.ScanId);
            Check(!File.Exists(path) && restored.Pending is null && restored.StartedAt is null && !restored.CanSubmit,
                "only matching confirmation clears file and input");
            int calls = handler.Calls;
            try { await api.UploadAsync(new Uri("http://example.test/"), "test-key", pending); throw new Exception("HTTP accepted"); }
            catch (ArgumentException) { }
            Check(handler.Calls == calls, "invalid endpoint blocked before credentials sent");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        Console.WriteLine("PASS: desktop scan timing, clear/reset, length, persistence/restart, immutable retry, API path/auth and confirmation checks.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task MustFail(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Expected upload failure");
    }

    private sealed class RepairHandler : HttpMessageHandler
    {
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool WrongId;
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Check(request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/wms/api/repair-scans", "upload endpoint");
            Check(request.Headers.GetValues("X-Upload-Key").Single() == "test-key", "authentication header");
            var scan = JsonSerializer.Deserialize<RepairScan>(await request.Content!.ReadAsStringAsync(ct), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            return new HttpResponseMessage(Status) { Content = new StringContent(JsonSerializer.Serialize(new
                { scanId = WrongId ? Guid.NewGuid() : scan.ScanId, status = "开始维修" })) };
        }
    }
}
