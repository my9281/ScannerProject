using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using System.Configuration;

namespace Scanner.WPF.Services
{
    public enum BluettiExportStage { PreparingPage, GeneratingFile, DownloadingFile }

    public sealed class BluettiExportTimeoutException : TimeoutException
    {
        public BluettiExportStage Stage { get; }
        public BluettiExportTimeoutException(BluettiExportStage stage) : base("BLUETTI export timed out: " + stage) { Stage = stage; }
    }

    public sealed class BluettiExportTimeouts
    {
        public TimeSpan Page { get; set; } = TimeSpan.FromSeconds(180);
        public TimeSpan ForecastGeneration { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan BaseGeneration { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan Download { get; set; } = TimeSpan.FromMinutes(15);

        public static BluettiExportTimeouts FromConfig()
        {
            double Read(string key, double fallback) => double.TryParse(ConfigurationManager.AppSettings[key],
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) &&
                double.IsFinite(value) && value > 0 && value <= 1440 ? value : fallback;
            return new BluettiExportTimeouts
            {
                Page = TimeSpan.FromSeconds(Read("BluettiExportPageTimeoutSeconds", 180)),
                ForecastGeneration = TimeSpan.FromMinutes(Read("BluettiForecastGenerationTimeoutMinutes", 10)),
                BaseGeneration = TimeSpan.FromMinutes(Read("BluettiBaseGenerationTimeoutMinutes", 30)),
                Download = TimeSpan.FromMinutes(Read("BluettiDownloadTimeoutMinutes", 15))
            };
        }
    }

    // Uses the same visible export buttons as the website, including its current download protocol.
    public sealed class BluettiBrowserExporter
    {
        public const string ForecastRoute = "/afterSaleClient/refactorWorkOrder/refactorCustomerWorkOrder/client/afterSaleWorkOrder/route/v2/index";
        public const string BaseRoute = "/afterSaleClient/maintenance/client/warehousingInspection/route/v2/index";
        private readonly WebView2 _browser;
        private readonly BluettiExportTimeouts _timeouts;
        private readonly Action<string> _trace;
        public BluettiBrowserExporter(WebView2 browser, BluettiExportTimeouts timeouts = null, Action<string> trace = null)
        { _browser = browser; _timeouts = timeouts ?? BluettiExportTimeouts.FromConfig(); _trace = trace; }
        private void Trace(string message) { BluettiDiagnostics.Write(message); _trace?.Invoke(message); }

        public async Task<string> ExportAsync(BluettiSession session, bool forecast, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_timeouts.Page);
            var stage = BluettiExportStage.PreparingPage;
            Trace("Export start: " + (forecast ? "forecast" : "base"));
            var token = timeout.Token;
            if (_browser.CoreWebView2 == null)
            {
                var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scanner", "Bluetti", "Browser");
                var environment = await CoreWebView2Environment.CreateAsync(null, profile).WaitAsync(TimeSpan.FromSeconds(30), token);
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.IsInPrivateModeEnabled = true;
                await _browser.EnsureCoreWebView2Async(environment, options).WaitAsync(TimeSpan.FromSeconds(30), token);
                _browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                _browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                _browser.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
                _browser.CoreWebView2.NavigationStarting += (_, e) =>
                {
                    if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) ||
                        uri.Scheme != "https" || uri.Host != "after-sales.bluettipower.com") e.Cancel = true;
                };
            }
            var core = _browser.CoreWebView2;
            var destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Scanner", "Bluetti", "Downloads", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            var file = Path.Combine(destination, forecast ? "DTC.xlsx" : "InboundDetection.xlsx");
            var finished = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CoreWebView2DownloadOperation operation = null;
            EventHandler<object> stateChanged = null;
            EventHandler<CoreWebView2DownloadStartingEventArgs> downloadStarting = (_, e) =>
            {
                if (operation != null) { e.Cancel = true; return; }
                e.ResultFilePath = file;
                e.Handled = true;
                operation = e.DownloadOperation;
                stage = BluettiExportStage.DownloadingFile;
                Trace("Download started.");
                timeout.CancelAfter(_timeouts.Download);
                stateChanged = (_, _) =>
                {
                    if (operation.State == CoreWebView2DownloadState.Completed) finished.TrySetResult(file);
                    else if (operation.State == CoreWebView2DownloadState.Interrupted)
                        finished.TrySetException(new IOException("Download interrupted: " + operation.InterruptReason));
                };
                operation.StateChanged += stateChanged;
                stateChanged(null, EventArgs.Empty);
            };
            // Values are JSON-encoded and injected only into this origin before application initialization.
            var storage = JsonConvert.SerializeObject(new
            {
                accessToken = session.Token,
                resource = session.SiteId,
                siteInfo = new { siteId = session.SiteId, siteName = session.SiteName }
            });
            var runId = Guid.NewGuid().ToString("N");
            string script = "if (location.origin === 'https://after-sales.bluettipower.com' && window === window.top) { " +
                "window.__scannerExportRunId=" + JsonConvert.SerializeObject(runId) + ";" +
                "sessionStorage.clear(); const values = " + storage + "; for (const [k,v] of Object.entries(values)) sessionStorage.setItem(k,JSON.stringify(v)); }";
            var scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
            core.DownloadStarting += downloadStarting;
            bool succeeded = false;
            try
            {
                // A full document navigation resets any filters left from the other export.
                var exportUrl = "https://after-sales.bluettipower.com/index.html?scannerSync=" + runId + "#" +
                    (forecast ? ForecastRoute : BaseRoute);
                core.Navigate(exportUrl);
                var label = JsonConvert.SerializeObject(forecast ? "DTC客诉单导出" : "导出");
                var expectedResource = JsonConvert.SerializeObject(JsonConvert.SerializeObject(session.SiteId));
                string previousState = null;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var result = await core.ExecuteScriptAsync("(() => { if(location.pathname.includes('login.html')) return 'login';" +
                        "if(window.__scannerExportRunId !== " + JsonConvert.SerializeObject(runId) + ") return 'waiting-navigation';" +
                        "if(location.origin !== 'https://after-sales.bluettipower.com' || sessionStorage.getItem('resource') !== " + expectedResource + ") return 'waiting-session';" +
                        "const clean=t=>t.replace(/[\\uE000-\\uF8FF\\s]/g,'');" +
                        "if(!location.hash.includes(" + JsonConvert.SerializeObject(forecast ? "afterSaleWorkOrder" : "warehousingInspection") + ")) {" +
                        "const link=[...document.querySelectorAll('a')].find(a=>a.getClientRects().length && (a.getAttribute('href')||'').includes(" +
                        JsonConvert.SerializeObject(forecast ? "/refactorCustomerWorkOrder/afterSaleWorkOrder" : "/maintenance/warehousingInspection") + "));" +
                        "if(link){link.click();return 'selecting-export-page';}" +
                        "const menu=[...document.querySelectorAll('a,span,li,div')].filter(e=>e.getClientRects().length && clean(e.innerText||'')===" +
                        JsonConvert.SerializeObject(forecast ? "工单管理" : "维修管理") + ").sort((a,b)=>a.children.length-b.children.length)[0];" +
                        "if(menu&&!window.__scannerMenuSelected){window.__scannerMenuSelected=true;menu.click();return 'selecting-menu';}" +
                        "return 'waiting-menu';}" +
                        "const b = [...document.querySelectorAll('button')].find(b => b.innerText.replace(/[\\uE000-\\uF8FF\\s]/g,'') === " + label + " && b.getClientRects().length && !b.disabled);" +
                        "if(!b) return 'waiting-button'; b.click(); return 'clicked'; })()");
                    var state = JsonConvert.DeserializeObject<string>(result);
                    if (state != previousState)
                    {
                        Trace("Page state: " + state);
                        previousState = state;
                        if (state == "waiting-button") Trace("Export labels: " + await core.ExecuteScriptAsync(
                            "[...document.querySelectorAll('button')].map(b=>b.innerText.replace(/[\\uE000-\\uF8FF\\s]/g,'')).filter(t=>/导出|export/i.test(t))"));
                    }
                    if (state == "login") throw new BluettiAuthenticationException();
                    if (state == "clicked")
                    {
                        if (operation == null)
                        {
                            stage = BluettiExportStage.GeneratingFile;
                            Trace("Waiting for server generation.");
                            timeout.CancelAfter(forecast ? _timeouts.ForecastGeneration : _timeouts.BaseGeneration);
                        }
                        break;
                    }
                    await Task.Delay(500, token);
                }
                var path = await finished.Task.WaitAsync(token);
                Trace("Download complete; bytes=" + new FileInfo(path).Length);
                // Reject HTML/JSON responses saved under an Excel filename before touching local datasets.
                using (var stream = File.OpenRead(path))
                {
                    if (stream.ReadByte() != 'P' || stream.ReadByte() != 'K') throw new InvalidDataException("Export is not an XLSX workbook.");
                }
                succeeded = true;
                return path;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Trace("Timed out: " + stage);
                throw new BluettiExportTimeoutException(stage);
            }
            catch (Exception ex)
            {
                Trace("Export failure at " + stage + ": " + ex);
                throw;
            }
            finally
            {
                core.DownloadStarting -= downloadStarting;
                core.RemoveScriptToExecuteOnDocumentCreated(scriptId);
                if (operation != null)
                {
                    operation.StateChanged -= stateChanged;
                    if (operation.State == CoreWebView2DownloadState.InProgress) operation.Cancel();
                }
                if (!succeeded) new BluettiDownloadRetention().Discard(file);
            }
        }
    }
}
