using System.Net;
using System.Text;
using Scanner.ShippingCowAndroid.Services;
using Scanner.Server.Model;

static class ReceivingConnectionChecks
{
    public static async Task Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        AppSettings Parse(string body) { using var xml = new MemoryStream(Encoding.UTF8.GetBytes(body)); return AppSettings.Parse(xml); }
        var settings = Parse("""
            <configuration><appSettings><add key="UploadBaseUrl" value="https://wms.ymforever.com"/>
            <add key="UploadApiKey" value="api-key"/><add key="ReceivingUsername" value=" PDA01 "/>
            <add key="ReceivingPassword" value=" pass&amp;&lt;&quot; "/></appSettings></configuration>
            """);
        Check(settings.ReceivingUsername == "PDA01" && settings.ReceivingPassword == " pass&<\" ", "config escaping and significant password whitespace");
        Check(settings.UploadBaseUrl.AbsoluteUri == "https://wms.ymforever.com/", "default deployment address");
        Check(!settings.ToString().Contains("pass&") && !settings.ToString().Contains("api-key"), "settings diagnostic text does not disclose credentials");
        var old = Parse("""<configuration><appSettings><add key="UploadBaseUrl" value="https://wms.ymforever.com/"/><add key="UploadApiKey" value="key"/></appSettings></configuration>""");
        Check(old.ReceivingUsername == "PDA01" && old.ReceivingPassword == "", "legacy config loads without blocking other scanner features");
        var api = new FakeApi(); var connection = new ConfiguredReceivingConnection(api, settings);
        var login = await connection.Connect(default);
        Check(api.LoginCalls == 1 && api.LastUsername == "PDA01" && api.LastPassword == settings.ReceivingPassword, "automatic configured login");
        await connection.Connect(default);
        Check(api.LoginCalls == 1 && api.ContextCalls == 1, "valid session revalidated without repeated login");
        await connection.PrepareUpload(default);
        Check(api.ContextCalls == 1 && api.LoginCalls == 1, "submit uses existing authenticated session without extra context fetch");
        var restored = new ConfiguredReceivingConnection(api, settings);
        Check(!restored.Restore(login with { Context = login.Context with { Username = "other" } }), "cache cannot switch configured username");
        Check(!restored.Restore(login with { Context = login.Context with { Server = "https://other.example/" } }), "cache cannot switch server");
        Check(restored.Restore(login), "same configured identity restores");
        api.ContextError = HttpStatusCode.Unauthorized; await restored.Connect(default);
        Check(api.LoginCalls == 2, "expired context automatically renews");
        api.ContextError = null;
        var expired = new ConfiguredReceivingConnection(api, settings);
        expired.Restore(login with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        await expired.Connect(default); Check(api.LoginCalls == 3, "expired cache reauthenticates");
        var missing = new ConfiguredReceivingConnection(api, old);
        try { await missing.Connect(default); throw new Exception("Blank configured password accepted"); }
        catch (InvalidOperationException ex) { Check(ex.Message.Contains("ReceivingPassword") && api.LoginCalls == 3, "blank configuration rejected without network login"); }
        var scan = new CarrierReceiptScan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "batch", 7, 42, "site-1", "fedex", "123456789012", "123456789012", DateTimeOffset.UtcNow);
        var batch = new CarrierReceiptBatch([scan]);
        api.UploadErrors.Enqueue(HttpStatusCode.Unauthorized);
        await restored.Upload(batch, default);
        Check(api.UploadCalls == 2 && api.LoginCalls == 4 && api.Sent.All(b => ReferenceEquals(b, batch)), "upload 401 retries exactly once with original immutable batch");
        api.UploadErrors.Enqueue(HttpStatusCode.Unauthorized); api.UploadErrors.Enqueue(HttpStatusCode.Unauthorized);
        try { await restored.Upload(batch, default); throw new Exception("Repeated 401 ignored"); }
        catch (CarrierReceivingApiException) { Check(api.UploadCalls == 4 && api.LoginCalls == 5 && restored.Session is null, "second 401 propagates and suspends rejected session"); }
        await restored.Connect(default);
        api.UploadErrors.Enqueue(HttpStatusCode.Forbidden); int beforeLogin = api.LoginCalls;
        try { await restored.Upload(batch, default); throw new Exception("403 ignored"); }
        catch (CarrierReceivingApiException) { Check(api.LoginCalls == beforeLogin && restored.Session is null, "permission rejection does not loop login"); }
        await restored.Connect(default); api.ContextError = HttpStatusCode.Forbidden; beforeLogin = api.LoginCalls;
        try { await restored.Connect(default); throw new Exception("Forbidden context ignored"); }
        catch (CarrierReceivingApiException) { Check(api.LoginCalls == beforeLogin && restored.Session is null, "context 403 stops scanning identity"); }
        api.ContextError = null;
        api.UserId = 99; await restored.Connect(default); int beforeUpload = api.UploadCalls;
        try { await restored.Upload(batch, default); throw new Exception("Queued scan reassigned"); }
        catch (InvalidOperationException) { Check(api.UploadCalls == beforeUpload, "new identity cannot upload old queued ownership"); }
        api.UserId = 42;
        restored.Restore(login);
        await restored.Connect(default);
        api.ThrowNetwork = true;
        try { await restored.Connect(default); throw new Exception("Expected offline state"); }
        catch (HttpRequestException) { Check(restored.Session?.Context.UserId == 42, "network outage preserves validated identity for offline scans"); }
        Console.WriteLine($"PASS: {checks} configured PDA connection checks (no external service access).");
    }
    sealed class FakeApi : ICarrierReceivingApi
    {
        public int LoginCalls, ContextCalls, UploadCalls;
        public ulong UserId = 42;
        public string? LastUsername, LastPassword;
        public HttpStatusCode? ContextError;
        public bool ThrowNetwork;
        public Queue<HttpStatusCode> UploadErrors = new();
        public List<CarrierReceiptBatch> Sent = new();
        private CarrierReceiptContext Identity => new(7, UserId, "PDA01", "site-1", "https://wms.ymforever.com/");
        public Task<ReceivingLogin> Login(string username, string password, CancellationToken ct)
        { LoginCalls++; LastUsername = username; LastPassword = password; return Task.FromResult(new ReceivingLogin("token-" + LoginCalls, DateTimeOffset.UtcNow.AddHours(1), Identity)); }
        public Task<CarrierReceiptContext> Context(string token, CancellationToken ct)
        {
            ContextCalls++;
            if (ThrowNetwork) throw new HttpRequestException("offline");
            if (ContextError is { } error) throw new CarrierReceivingApiException(error, "rejected");
            return Task.FromResult(Identity);
        }
        public Task<CarrierReceiptAcknowledgement> Upload(string token, CarrierReceiptBatch batch, CancellationToken ct)
        {
            UploadCalls++; Sent.Add(batch);
            if (UploadErrors.Count > 0) throw new CarrierReceivingApiException(UploadErrors.Dequeue(), "rejected");
            return Task.FromResult(new CarrierReceiptAcknowledgement(batch.Items.Select(s => s.ScanId).ToArray(), batch.Items.Count));
        }
    }
}
