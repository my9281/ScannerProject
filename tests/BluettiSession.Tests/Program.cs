using System.Net;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scanner.WPF;
using Scanner.WPF.Helpers;
using Scanner.WPF.Services;
using Scanner.Helpers;
using Scanner.Helpers.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        CheckSessionAsync().GetAwaiter().GetResult();
        CheckConfiguration();
        CheckSyncConfigurationAsync().GetAwaiter().GetResult();
        CheckDirectApiAsync().GetAwaiter().GetResult();
        if (args.Length == 2)
        {
            var forecast = new UrgentWorkOrderImportHelper().Import(args[0]);
            Check(forecast.ImportedRowCount > 0 && forecast.Rules.Count > 0, "DTC export has no usable forecast rules.");
            var search = new WorkOrderSearchService();
            search.ReplaceImported(forecast.Rules);
            var cache = new ChecklistDataCache();
            cache.ImportBase(args[1]);
            Check(cache.Records.Count > 0 && cache.MonthlySource != null, "Inspection export did not populate shared base data.");
            Console.WriteLine($"PASS: real exports import: forecast {forecast.ImportedRowCount} rows; base {cache.Records.Count} records.");
            CheckSyncRetentionAsync(args[0], args[1]).GetAwaiter().GetResult();
        }
        if (args.Length == 1 && args[0] == "--verify-live-api")
        {
            VerifyLiveApiAsync().GetAwaiter().GetResult();
            return;
        }
        var app = new Scanner.WPF.App();
        app.InitializeComponent();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var frame = new DispatcherFrame();
        var capture = args.Length == 1 && args[0] == "--capture-export-api";
        var live = capture || (args.Length == 1 && (args[0] == "--diagnose-live-base" || args[0] == "--diagnose-live-sync"));
        var browserTest = capture ? CaptureExportRequestsAsync() : live ? DiagnoseLiveBaseAsync(args[0] == "--diagnose-live-sync") : CheckBrowserExporterAsync();
        browserTest.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        Dispatcher.PushFrame(frame);
        browserTest.GetAwaiter().GetResult();
        if (live) return;
        foreach (var language in new[] { "zh-CN", "en-US", "es-ES" })
        {
            UiText.ChangeLanguage(language);
            foreach (var settingsMode in new[] { false, true })
            {
            var window = new BluettiSessionWindow(null, null, settingsMode);
            var content = (FrameworkElement)window.Content;
            int width = (int)window.Width - 16, height = (int)window.Height - 39;
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
                drawing.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, width, height));
            }
            image.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(Path.Combine(AppContext.BaseDirectory, (settingsMode ? "settings-" : "bluetti-") + language + ".png"));
            encoder.Save(file);
            }
        }
        Console.WriteLine("PASS: encrypted session round trip, malformed storage, protocol, site header, expiry, three language renders.");
    }

    static void CheckConfiguration()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BluettiConfigTest-" + Guid.NewGuid().ToString("N"));
        var store = new BluettiConfigStore(Path.Combine(folder, "bluetti.config"));
        try
        {
            Check(store.Load() == null, "Missing config should be empty.");
            store.Save(new BluettiLoginConfig { Username = "test@example.com", Password = "test-secret-<&>", SiteId = "site-1" });
            Check(!File.ReadAllText(store.FilePath).Contains("test-secret"), "Plaintext password found in config.");
            var restored = store.Load();
            Check(restored.Username == "test@example.com" && restored.Password == "test-secret-<&>" && restored.SiteId == "site-1", "Config round trip failed.");
            store.Save(new BluettiLoginConfig { Username = "another@example.com", Password = "replacement", SiteId = "site-2" });
            Check(store.Load().Username == "another@example.com" && store.Load().SiteId == "site-2", "Config update failed.");
            store.Clear();
            Check(store.Load() == null, "Config removal failed.");
            Console.WriteLine("PASS: external config create/update/remove and DPAPI password round trip.");
        }
        finally { store.Clear(); if (Directory.Exists(folder)) Directory.Delete(folder); }
    }

    static async Task CheckSessionAsync()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ScannerBluettiTests-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "session.dat");
        using var client = new HttpClient(new FakeGateway());
        var service = new BluettiSessionService(client, file);
        try
        {
            var session = await service.LoginAsync(" user@example.com ", "test-password", default);
            var sites = await service.GetSitesAsync(session, default);
            Check(sites.Count == 1 && sites[0].SiteId == "site-1", "Disabled site filtering failed.");
            await service.SelectSiteAsync(session, sites[0], default);
            service.Save(session);
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(file)).Contains("test-token"), "Plaintext token on disk.");
            var restored = service.Load();
            Check(restored.Token == "test-token" && restored.SiteId == "site-1", "Session round trip failed.");
            await service.ValidateAsync(restored, default);
            File.WriteAllText(file, "damaged-session");
            Check(service.Load() == null && !File.Exists(file), "Damaged storage was not cleared.");
            using var expiredClient = new HttpClient(new FakeGateway { Expired = true });
            try
            {
                await new BluettiSessionService(expiredClient, file).ValidateAsync(session, default);
                throw new Exception("Expired token was accepted.");
            }
            catch (BluettiAuthenticationException) { }
        }
        finally
        {
            service.Clear();
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
    }

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    static async Task VerifyLiveApiAsync()
    {
        var sync = new BluettiSyncService();
        var session = await sync.PrepareAsync(default);
        var result = await sync.ImportAsync(session, new BluettiApiExporter().ExportAsync,
            new UrgentWorkOrderImportHelper().Import, new ChecklistDataCache(),
            r => Console.WriteLine($"DIRECT API: {r.State}; forecast={r.ForecastCount}; base={r.BaseCount}; error={r.ErrorCode}; timeout={r.TimedOutStage}"), default);
        Check(result.State == BluettiSyncState.Succeeded, "Direct API sync failed.");
    }

    static async Task CheckDirectApiAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "BluettiApiTest-" + Guid.NewGuid().ToString("N"));
        var retention = new BluettiDownloadRetention(root);
        var session = new BluettiSession { Token = "test-token", SiteId = "site-1" };
        using var workbook = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(workbook, System.IO.Compression.ZipArchiveMode.Create, true))
        using (var text = new StreamWriter(zip.CreateEntry("xl/workbook.xml").Open())) text.Write("<workbook/>");
        var bytes = workbook.ToArray();
        var files = new List<string>();
        try
        {
            using var client = new HttpClient(new ApiHandler(async (request, token) =>
            {
                Check(request.Method == HttpMethod.Post && request.RequestUri.Host == "gwpry.bluettipower.com", "Wrong export gateway/method.");
                Check(request.Headers.GetValues("Authorization").Single() == session.Token, "Missing raw auth token.");
                Check(request.Headers.GetValues("x-app-resource").Single() == session.SiteId, "Missing site.");
                var payload = JObject.Parse(await request.Content.ReadAsStringAsync(token));
                if (request.RequestUri.AbsolutePath == BluettiApiExporter.ForecastPath)
                    Check((string)payload["siteId"] == "site-1" && (int)payload["state"] == 1 && (int)payload["type"] == 1 && payload["idList"] is JArray, "Wrong captured DTC filters.");
                else
                    Check(request.RequestUri.AbsolutePath == BluettiApiExporter.BasePath && payload["inpectionResultList"] is JArray && payload.Count == 4, "Wrong captured base payload.");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            }));
            var exporter = new BluettiApiExporter(client, retention: retention);
            foreach (var forecast in new[] { true, false })
            {
                var file = await exporter.ExportAsync(session, forecast, default); files.Add(file);
                Check(File.ReadAllBytes(file).SequenceEqual(bytes), "Direct export corrupted the file.");
            }
            using var denied = new HttpClient(new ApiHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"msgCode\":800}", Encoding.UTF8, "application/json") })));
            try { await new BluettiApiExporter(denied, retention: retention).ExportAsync(session, false, default); throw new Exception("JSON auth error accepted as Excel."); }
            catch (BluettiAuthenticationException) { }
            using var invalid = new HttpClient(new ApiHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("<html>error</html>") })));
            try { await new BluettiApiExporter(invalid, retention: retention).ExportAsync(session, false, default); throw new Exception("HTML error accepted as Excel."); }
            catch (InvalidDataException) { }
            using var slow = new HttpClient(new ApiHandler(async (_, ct) => { await Task.Delay(5000, ct); return new HttpResponseMessage(HttpStatusCode.OK); }));
            try { await new BluettiApiExporter(slow, new BluettiExportTimeouts { BaseGeneration = TimeSpan.FromMilliseconds(30) }, retention).ExportAsync(session, false, default); throw new Exception("Missing generation deadline."); }
            catch (BluettiExportTimeoutException ex) { Check(ex.Stage == BluettiExportStage.GeneratingFile, "Wrong direct API timeout phase."); }
            using var cancelled = new CancellationTokenSource(30);
            try { await new BluettiApiExporter(slow, retention: retention).ExportAsync(session, false, cancelled.Token); throw new Exception("Caller cancellation ignored."); }
            catch (OperationCanceledException) { }
            Console.WriteLine("PASS: captured API methods/payloads/headers, direct streaming downloads, JSON/HTML rejection, generation timeout and cancellation.");
        }
        finally
        {
            foreach (var file in files) retention.Discard(file);
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }

    private sealed class ApiHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
        public ApiHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { _send = send; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _send(request, cancellationToken);
    }

    static async Task CaptureExportRequestsAsync()
    {
        using var browser = new WebView2 { Width = 1200, Height = 800, IsHitTestVisible = false };
        var host = new Window { Content = browser, Width = 1200, Height = 800, ShowInTaskbar = false, Left = -2000 };
        host.Show();
        var records = new Dictionary<string, JObject>();
        var output = Path.Combine(AppContext.BaseDirectory, "export-requests.json");
        try
        {
            var session = await new BluettiSyncService().PrepareAsync(default);
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "ScannerApiCapture-" + Guid.NewGuid().ToString("N")));
            var options = environment.CreateCoreWebView2ControllerOptions(); options.IsInPrivateModeEnabled = true;
            await browser.EnsureCoreWebView2Async(environment, options);
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{\"maxPostDataSize\":65536}");
            browser.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.requestWillBeSent").DevToolsProtocolEventReceived += (_, e) =>
            {
                var message = JObject.Parse(e.ParameterObjectAsJson);
                var request = message["request"];
                if (!Uri.TryCreate((string)request?["url"], UriKind.Absolute, out var uri) || !uri.AbsolutePath.Contains("/api/") ||
                    !uri.AbsolutePath.Contains("export", StringComparison.OrdinalIgnoreCase)) return;
                var headers = new JObject();
                foreach (var pair in ((JObject)request["headers"]).Properties())
                    if (new[] { "content-type", "x-app-key", "x-os", "x-app-resource", "accept-language" }.Contains(pair.Name.ToLowerInvariant())) headers[pair.Name] = pair.Value;
                var record = new JObject { ["method"] = request["method"], ["url"] = uri.GetLeftPart(UriPartial.Path),
                    ["query"] = uri.Query, ["body"] = request["postData"], ["headers"] = headers };
                records[(string)message["requestId"]] = record;
                File.WriteAllText(output, new JArray(records.Values).ToString());
                Console.WriteLine("CAPTURE REQUEST: " + record);
            };
            browser.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.responseReceived").DevToolsProtocolEventReceived += (_, e) =>
            {
                var message = JObject.Parse(e.ParameterObjectAsJson);
                if (!records.TryGetValue((string)message["requestId"], out var record)) return;
                record["status"] = message["response"]?["status"];
                record["mimeType"] = message["response"]?["mimeType"];
                File.WriteAllText(output, new JArray(records.Values).ToString());
                Console.WriteLine("CAPTURE RESPONSE: status=" + record["status"] + "; type=" + record["mimeType"]);
            };
            var exporter = new BluettiBrowserExporter(browser, trace: Console.WriteLine);
            foreach (var forecast in new[] { true, false })
            {
                var file = await exporter.ExportAsync(session, forecast, default);
                if (forecast) Check(new UrgentWorkOrderImportHelper().Import(file).ImportedRowCount > 0, "Captured forecast failed import.");
                else { var cache = new ChecklistDataCache(); cache.ImportBase(file); Check(cache.Records.Count > 0, "Captured base failed import."); }
                new BluettiDownloadRetention().KeepLatest(file, forecast);
            }
            Check(records.Count >= 2, "Both export requests were not captured.");
            Console.WriteLine("CAPTURE COMPLETE: " + output);
        }
        finally { host.Close(); }
    }

    static async Task DiagnoseLiveBaseAsync(bool both)
    {
        using var browser = new WebView2 { Width = 1200, Height = 800, IsHitTestVisible = false };
        var host = new Window { Content = new System.Windows.Controls.Grid { IsEnabled = false, Children = { browser } }, Width = 1200, Height = 800, ShowInTaskbar = false, Left = -2000 };
        host.Show();
        try
        {
            Console.WriteLine("LIVE: checking saved session/configuration.");
            var session = await new BluettiSyncService().PrepareAsync(default);
            if (both)
            {
                var exporter = new BluettiBrowserExporter(browser, trace: Console.WriteLine);
                var result = await new BluettiSyncService().ImportAsync(session, exporter.ExportAsync,
                    new UrgentWorkOrderImportHelper().Import, new ChecklistDataCache(),
                    r => Console.WriteLine($"LIVE STATE: {r.State}; forecast={r.ForecastCount}; base={r.BaseCount}; error={r.ErrorCode}; timeout={r.TimedOutStage}"), default);
                Check(result.State == BluettiSyncState.Succeeded, "Live sequential sync failed.");
                return;
            }
            Console.WriteLine("LIVE: starting base export.");
            var export = new BluettiBrowserExporter(browser, trace: Console.WriteLine).ExportAsync(session, false, default);
            await Task.Delay(10000);
            if (!export.IsCompleted)
            {
                Console.WriteLine("LIVE PAGE: " + await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({ready:document.readyState,buttons:[...document.querySelectorAll('button')].map(b=>b.innerText.replace(/[\\uE000-\\uF8FF\\s]/g,'')).filter(t=>/导出|export/i.test(t)),appChildren:document.querySelector('#app')?.children.length})"));
                using var preview = File.Create(Path.Combine(AppContext.BaseDirectory, "live-base.png"));
                await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, preview);
            }
            var file = await export;
            Console.WriteLine("LIVE: download complete; validating import.");
            var cache = new ChecklistDataCache();
            cache.ImportBase(file);
            Console.WriteLine("LIVE: base import succeeded; rows=" + cache.Records.Count);
            new BluettiDownloadRetention().KeepLatest(file, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine("LIVE FAILURE: " + ex);
            throw;
        }
        finally { host.Close(); }
    }

    static async Task CheckSyncConfigurationAsync()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BluettiPrepareTest-" + Guid.NewGuid().ToString("N"));
        using var client = new HttpClient(new FakeGateway());
        var sessions = new BluettiSessionService(client, Path.Combine(folder, "session.dat"));
        var config = new BluettiConfigStore(Path.Combine(folder, "bluetti.config"));
        var sync = new BluettiSyncService(sessions, config);
        try
        {
            try { await sync.PrepareAsync(default); throw new Exception("Missing configuration accepted."); }
            catch (BluettiConfigurationException) { }
            config.Save(new BluettiLoginConfig { Username = "user@example.com", Password = "test-password", SiteId = "disabled" });
            try { await sync.PrepareAsync(default); throw new Exception("Disabled site accepted."); }
            catch (BluettiConfigurationException) { }
            config.Save(new BluettiLoginConfig { Username = "user@example.com", Password = "test-password", SiteId = "site-1" });
            Check((await sync.PrepareAsync(default)).SiteId == "site-1", "Valid configuration failed.");
            File.WriteAllText(config.FilePath, "invalid config");
            try { await sync.PrepareAsync(default); throw new Exception("Malformed config accepted."); }
            catch (BluettiConfigurationException) { }
            Console.WriteLine("PASS: configuration missing/malformed/disabled-site branches and configured login.");
        }
        finally { sessions.Clear(); config.Clear(); if (Directory.Exists(folder)) Directory.Delete(folder); }
    }

    static async Task CheckSyncRetentionAsync(string forecastSource, string baseSource)
    {
        var root = Path.Combine(Path.GetTempPath(), "BluettiRetentionTest-" + Guid.NewGuid().ToString("N"));
        var retention = new BluettiDownloadRetention(root);
        var sync = new BluettiSyncService(retention: retention);
        var created = new List<string>();
        string Copy(bool forecast, bool invalid = false)
        {
            var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, forecast ? "DTC.xlsx" : "InboundDetection.xlsx");
            if (invalid) File.WriteAllText(file, "invalid workbook");
            else File.Copy(forecast ? forecastSource : baseSource, file);
            created.Add(file);
            return file;
        }
        try
        {
            var oldForecast = Copy(true);
            var oldBase = Copy(false);
            var cache = new ChecklistDataCache();
            var importer = new UrgentWorkOrderImportHelper();
            var result = await sync.ImportAsync(new BluettiSession(), (_, f, _) => Task.FromResult(Copy(f)), importer.Import, cache, null, default);
            Check(result.State == BluettiSyncState.Succeeded && result.ForecastCount > 0 && result.BaseCount > 0, "Success counts missing.");
            Check(!File.Exists(oldForecast) && !File.Exists(oldBase), "Superseded workbooks were not deleted.");
            var previousBase = created.Last();
            int oldCount = cache.Records.Count;
            result = await sync.ImportAsync(new BluettiSession(), (_, f, _) => Task.FromResult(Copy(f, !f)), importer.Import, cache, null, default);
            Check(result.State == BluettiSyncState.Failed && result.ForecastImported && !result.BaseImported, "Partial failure state lost.");
            Check(File.Exists(previousBase) && cache.Records.Count == oldCount, "Failed import removed the previous base data.");
            Check(!File.Exists(created.Last()), "Failed workbook was retained.");
            Check(Directory.EnumerateFiles(root, "*.xlsx", SearchOption.AllDirectories).Count() == 2, "Expected only latest two successful workbooks.");
            var restartedCache = new ChecklistDataCache();
            var restored = new BluettiSyncService(retention: new BluettiDownloadRetention(root)).RestoreCached(importer.Import, restartedCache);
            Check(restored.State == BluettiSyncState.Succeeded && restored.ForecastCount == result.ForecastCount &&
                restored.BaseCount == oldCount && restartedCache.MonthlySource != null, "Restart did not restore cached datasets.");
            File.WriteAllText(retention.GetCachedFiles(true)[0], "damaged workbook");
            restored = sync.RestoreCached(importer.Import, new ChecklistDataCache());
            Check(restored.State == BluettiSyncState.Failed && !restored.ForecastImported && restored.BaseImported,
                "One damaged cache prevented independent restore of the other.");
            Check(!retention.Discard(forecastSource), "Cleanup escaped its managed directory.");
            Console.WriteLine("PASS: counts, retention, partial failure, restart cache restore, independent damaged-cache handling and path boundary.");
        }
        finally
        {
            foreach (var file in created) retention.Discard(file);
            File.Delete(Path.Combine(root, "latest-forecast.txt"));
            File.Delete(Path.Combine(root, "latest-base.txt"));
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }

    static async Task CheckBrowserExporterAsync()
    {
        var profile = Path.Combine(Path.GetTempPath(), "ScannerBluettiBrowserTest-" + Guid.NewGuid().ToString("N"));
        using var browser = new WebView2 { Width = 1200, Height = 800, IsHitTestVisible = false };
        var background = new System.Windows.Controls.Grid
        {
            Width = 1, Height = 1, Margin = new Thickness(-2000, -2000, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true, IsHitTestVisible = false, Children = { browser }
        };
        var host = new Window { Content = new System.Windows.Controls.Grid { IsEnabled = false, Children = { background } }, Width = 800, Height = 600, ShowInTaskbar = false, Left = -2000 };
        host.Show();
        var exported = new List<string>();
        try
        {
            Console.WriteLine("Browser test: initializing runtime.");
            var environment = await CoreWebView2Environment.CreateAsync(null, profile).WaitAsync(TimeSpan.FromSeconds(30));
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled = true;
            await browser.EnsureCoreWebView2Async(environment, options).WaitAsync(TimeSpan.FromSeconds(30));
            Console.WriteLine("Browser test: runtime ready.");
            browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            bool stallGeneration = false;
            browser.CoreWebView2.WebResourceRequested += (_, e) =>
            {
                Console.WriteLine("Browser test: serving offline page.");
                var html = "<html><body><span onclick=\"location.hash='/afterSaleClient/maintenance/client/warehousingInspection/route/v2/index';render()\">维修管理</span><script>" +
                    "const base=location.hash.includes('warehousingInspection');" +
                    "function render(){requestAnimationFrame(()=>{document.querySelectorAll('button').forEach(b=>b.remove());" +
                    "for(const label of ['DTC客诉单导出','导出']){const b=document.createElement('button');b.textContent=label;document.body.appendChild(b);" +
                    "b.onclick=()=>{" + (stallGeneration ? "return;" : "") +
                    "setTimeout(()=>{const blob=new Blob([new Uint8Array([80,75,3,4])],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'});" +
                    "const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='test.xlsx';a.click();},base?3500:0);};}});}" +
                    "if(base)location.hash='/default-workorders';else render();</script></body></html>";
                e.Response = environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(html)), 200, "OK", "Content-Type: text/html; charset=utf-8");
            };
            var timings = new BluettiExportTimeouts { Page = TimeSpan.FromSeconds(3), BaseGeneration = TimeSpan.FromSeconds(6), Download = TimeSpan.FromSeconds(3) };
            var exporter = new BluettiBrowserExporter(browser, timings);
            var session = new BluettiSession { Token = "test-token", SiteId = "test-site", SiteName = "Test site" };
            foreach (var forecast in new[] { true, false })
            {
                Console.WriteLine("Browser test: exporting " + forecast);
                var file = await exporter.ExportAsync(session, forecast, default);
                exported.Add(file);
                Check(File.ReadAllBytes(file).SequenceEqual(new byte[] { 80, 75, 3, 4 }), "Browser download was not captured.");
                Check(browser.Source.Fragment.Contains(forecast ? "afterSaleWorkOrder" : "warehousingInspection"), "Wrong export route.");
            }
            stallGeneration = true;
            timings.BaseGeneration = TimeSpan.FromMilliseconds(100);
            try { await exporter.ExportAsync(session, false, default); throw new Exception("Generation timeout was not detected."); }
            catch (BluettiExportTimeoutException ex) { Check(ex.Stage == BluettiExportStage.GeneratingFile, "Wrong timeout phase."); }
            Console.WriteLine("PASS: embedded browser navigates both routes, restores session storage, clicks export and captures downloads (offline test page).");
            Console.WriteLine("PASS: redirected route recovery, animation initialization, generation longer than page deadline and generation timeout phase.");
        }
        finally
        {
            host.Close();
            foreach (var file in exported)
            {
                File.Delete(file);
                Directory.Delete(Path.GetDirectoryName(file));
            }
        }
    }

    private sealed class FakeGateway : HttpMessageHandler
    {
        public bool Expired { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Check(request.RequestUri.Host == "gwpry.bluettipower.com", "Wrong gateway.");
            Check(request.Headers.GetValues("x-os").Single() == "web", "Missing client headers.");
            Check(request.Headers.GetValues("x-app-key").Single() == "18880A4BA6CF791157564C2ECD0", "Wrong app key.");
            string json;
            if (request.RequestUri.AbsolutePath == "/accessToken")
            {
                Check(request.Method == HttpMethod.Post, "Wrong login method.");
                var form = (MultipartFormDataContent)request.Content;
                var parts = new Dictionary<string, string>();
                foreach (var part in form) parts[part.Headers.ContentDisposition.Name.Trim('"')] = await part.ReadAsStringAsync(cancellationToken);
                Check(parts["username"] == "user@example.com", "Wrong username.");
                Check(parts["password"] == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-password"))), "Wrong password hash.");
                json = "{\"msgCode\":0,\"data\":{\"token\":\"test-token\"}}";
            }
            else
            {
                Check(request.Headers.GetValues("Authorization").Single() == "test-token", "Wrong authorization header.");
                json = request.RequestUri.AbsolutePath switch
                {
                    "/api/userInfo" => "{\"msgCode\":0,\"data\":{\"uid\":\"user-1\",\"nickname\":\"Test user\"}}",
                    "/api/bluas/client/user/v1/getUserSite" => "{\"msgCode\":0,\"data\":[{\"siteId\":\"site-1\",\"siteName\":\"Test site\",\"enable\":1},{\"siteId\":\"disabled\",\"enable\":0}]}",
                    "/api/bluas/client/role/v1/existPermissions" => "{\"msgCode\":0,\"data\":true}",
                    _ => throw new Exception("Unexpected endpoint.")
                };
                if (request.RequestUri.AbsolutePath.EndsWith("existPermissions"))
                    Check(request.Headers.GetValues("x-app-resource").Single() == "site-1", "Missing site header.");
                if (Expired) json = "{\"msgCode\":800}";
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
