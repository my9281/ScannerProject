using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.WPF.Services
{
    public sealed class ScanUploadService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public async Task<ScanUploadResult> UploadAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException(Scanner.WPF.Helpers.UiText.Get("WpfPrompt018"), filePath);
            }

            if (string.IsNullOrWhiteSpace(File.ReadAllText(filePath, Encoding.UTF8)))
            {
                throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfScanLogEmpty"));
            }

            using (var form = new MultipartFormDataContent())
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var fileContent = new StreamContent(stream))
            using (var request = new HttpRequestMessage(HttpMethod.Post, "api/uploads/scans"))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                form.Add(fileContent, "file", Path.GetFileName(filePath));
                form.Add(new StringContent(Environment.MachineName), "deviceName");
                request.Content = form;

                string uploadKey = ConfigurationManager.AppSettings["UploadApiKey"] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(uploadKey))
                {
                    request.Headers.Add("X-Upload-Key", uploadKey);
                }

                try
                {
                    using (HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            UploadError error = TryDeserialize<UploadError>(body);
                            throw new InvalidOperationException(error == null || string.IsNullOrWhiteSpace(error.Message)
                                ? Scanner.WPF.Helpers.UiText.Get("WpfUploadHttpError") + (int)response.StatusCode
                                : error.Message);
                        }

                        ScanUploadResult result = TryDeserialize<ScanUploadResult>(body);
                        if (result == null || string.IsNullOrWhiteSpace(result.FileName))
                        {
                            throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfPrompt019"));
                        }
                        return result;
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfPrompt020") + HttpClient.BaseAddress + Scanner.WPF.Helpers.UiText.Get("WpfPrompt021") + GetInnermostMessage(ex), ex);
                }
                catch (TaskCanceledException ex)
                {
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfPrompt022") + HttpClient.BaseAddress + Scanner.WPF.Helpers.UiText.Get("WpfPrompt023"), ex);
                }
            }
        }

        private static HttpClient CreateHttpClient()
        {
            string configuredUrl = ConfigurationManager.AppSettings["UploadBaseUrl"];
            string baseUrl = string.IsNullOrWhiteSpace(configuredUrl) ? "https://wms.ymforever.com/" : configuredUrl.Trim();
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }
            return new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        private static T TryDeserialize<T>(string json) where T : class
        {
            try { return JsonConvert.DeserializeObject<T>(json); }
            catch (JsonException) { return null; }
        }

        private static string GetInnermostMessage(Exception exception)
        {
            while (exception.InnerException != null) exception = exception.InnerException;
            return exception.Message;
        }

        private sealed class UploadError
        {
            public string Message { get; set; }
        }
    }

    public sealed class ScanUploadResult
    {
        public string FileName { get; set; }
        public string OriginalName { get; set; }
        public long Size { get; set; }
        public DateTimeOffset UploadedAtUtc { get; set; }
        public string Message { get; set; }
    }
}
