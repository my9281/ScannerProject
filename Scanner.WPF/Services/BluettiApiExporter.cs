using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Scanner.WPF.Services
{
    public sealed class BluettiApiExporter
    {
        public const string ForecastPath = "/api/bluas/client/workOrder/postSale/exportCustomerDemands";
        public const string BasePath = "/api/bluas/client/wareHousingInspection/exportInspection";
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
        { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
        private readonly HttpClient _client;
        private readonly BluettiExportTimeouts _timeouts;
        private readonly BluettiDownloadRetention _retention;

        public BluettiApiExporter(HttpClient client = null, BluettiExportTimeouts timeouts = null, BluettiDownloadRetention retention = null)
        { _client = client ?? Client; _timeouts = timeouts ?? BluettiExportTimeouts.FromConfig(); _retention = retention ?? new BluettiDownloadRetention(); }

        public static JObject BuildPayload(BluettiSession session, bool forecast)
        {
            if (!forecast) return new JObject
            {
                ["inpectionResultList"] = new JArray(), ["pageNo"] = 1, ["pageSize"] = 20, ["jobStatusList"] = new JArray()
            };
            var payload = new JObject();
            foreach (var key in new[] { "purchaseChannels", "isOverseasReceiving", "shippingOrderNo", "sn", "status", "afterStatus",
                "postSaleNo", "handler", "customerPhone", "customerName", "customerEmail", "afterSaleSource", "returnReason",
                "orderNo", "model", "rmaNo", "customerType", "afterSaleReason", "ticketId", "creBy", "orderByField", "orderBy" }) payload[key] = "";
            foreach (var key in new[] { "submitTime", "afterStatusList", "afterSaleTypeList", "dataSourceList", "idList" }) payload[key] = new JArray();
            payload["type"] = 1; payload["state"] = 1; payload["pageNo"] = 1; payload["pageSize"] = 20; payload["siteId"] = session.SiteId;
            return payload;
        }

        public async Task<string> ExportAsync(BluettiSession session, bool forecast, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(session?.Token) || string.IsNullOrWhiteSpace(session.SiteId)) throw new BluettiAuthenticationException();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(forecast ? _timeouts.ForecastGeneration : _timeouts.BaseGeneration);
            var token = deadline.Token;
            var stage = BluettiExportStage.GeneratingFile;
            var directory = Path.Combine(_retention.Root, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, forecast ? "DTC.xlsx" : "InboundDetection.xlsx");
            bool succeeded = false;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://gwpry.bluettipower.com" + (forecast ? ForecastPath : BasePath));
                request.Headers.Add("Authorization", session.Token);
                request.Headers.Add("x-app-resource", session.SiteId);
                request.Headers.Add("x-app-key", "18880A4BA6CF791157564C2ECD0");
                request.Headers.Add("x-os", "web");
                request.Headers.Add("Accept-Language", "zh-CN");
                request.Headers.Add("Origin", "https://after-sales.bluettipower.com");
                request.Headers.Referrer = new Uri("https://after-sales.bluettipower.com/");
                request.Content = new StringContent(BuildPayload(session, forecast).ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                BluettiDiagnostics.Write("API export start: " + (forecast ? "forecast" : "base"));
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) throw new BluettiAuthenticationException();
                response.EnsureSuccessStatusCode();
                using var stream = await response.Content.ReadAsStreamAsync(token);
                var first = new byte[2];
                await stream.ReadExactlyAsync(first, token);
                if (first[0] != 'P' || first[1] != 'K')
                {
                    if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // Read a bounded error envelope; never log response data or credentials.
                        var buffer = new byte[8192];
                        int read = await stream.ReadAsync(buffer, token);
                        try
                        {
                            var json = JObject.Parse(Encoding.UTF8.GetString(first) + Encoding.UTF8.GetString(buffer, 0, read));
                            int? code = (int?)json["msgCode"];
                            if (code >= 800 && code <= 999) throw new BluettiAuthenticationException();
                        }
                        catch (Newtonsoft.Json.JsonException) { }
                    }
                    throw new InvalidDataException("BLUETTI export did not return an XLSX workbook.");
                }
                // Server generation is complete only once file bytes actually arrive, not merely response headers.
                stage = BluettiExportStage.DownloadingFile;
                deadline.CancelAfter(_timeouts.Download);
                Directory.CreateDirectory(directory);
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await output.WriteAsync(first, token);
                    await stream.CopyToAsync(output, token);
                }
                using (var workbook = ZipFile.OpenRead(path))
                    if (workbook.GetEntry("xl/workbook.xml") == null) throw new InvalidDataException("Exported workbook is incomplete.");
                BluettiDiagnostics.Write("API export complete: " + (forecast ? "forecast" : "base") + "; bytes=" + new FileInfo(path).Length);
                succeeded = true;
                return path;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { throw new BluettiExportTimeoutException(stage); }
            finally { if (!succeeded) _retention.Discard(path); }
        }
    }
}
