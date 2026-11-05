using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Scanner.WPF.Services
{
    public sealed class FedExTrackingService : IDisposable
    {
        private readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };

        public async Task<JObject> TrackAsync(string number, CancellationToken cancellationToken)
        {
            var key = Environment.GetEnvironmentVariable("FEDEX_CLIENT_ID");
            var secret = Environment.GetEnvironmentVariable("FEDEX_CLIENT_SECRET");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("尚未配置 FedEx API。请设置 FEDEX_CLIENT_ID、FEDEX_CLIENT_SECRET 环境变量后重新启动程序。");
            var sandbox = string.Equals(Environment.GetEnvironmentVariable("FEDEX_SANDBOX"), "true", StringComparison.OrdinalIgnoreCase);
            var host = sandbox ? "https://apis-sandbox.fedex.com" : "https://apis.fedex.com";
            using var auth = await client.PostAsync(host + "/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials", ["client_id"] = key, ["client_secret"] = secret
            }), cancellationToken);
            if (!auth.IsSuccessStatusCode) throw new InvalidOperationException("FedEx 授权失败，请检查 API 凭据及使用环境。");
            var token = JObject.Parse(await auth.Content.ReadAsStringAsync(cancellationToken))["access_token"]?.ToString();
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("FedEx 未返回有效授权。");
            var body = new JObject
            {
                ["includeDetailedScans"] = true,
                ["trackingInfo"] = new JArray(new JObject { ["trackingNumberInfo"] = new JObject { ["trackingNumber"] = number } })
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, host + "/track/v1/trackingnumbers");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("FedEx 查询失败（HTTP " + (int)response.StatusCode + "），请稍后重试或检查 Track API 权限。");
            var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var result = json.SelectToken("output.completeTrackResults[0].trackResults[0]") as JObject;
            if (result == null) throw new InvalidOperationException("FedEx 未返回该运单的追踪记录。");
            if (result["error"] != null) throw new InvalidOperationException("FedEx：" + (result.SelectToken("error.message")?.ToString() ?? "未找到运单"));
            return result;
        }
        public void Dispose() => client.Dispose();
    }
}
