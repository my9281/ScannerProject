using Scanner.Models.MauiContracts;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Scanner.MaUI.Services;

public sealed class WorkOrderRemarkApiService
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("https://repair-rms.vercel.app"), Timeout = TimeSpan.FromSeconds(20) };
    public async Task<WorkOrderRemarkResponse> GetAsync(string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/work-orders/remarks?limit=500");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await _client.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"工单服务返回 HTTP {(int)response.StatusCode}。");
        return await response.Content.ReadFromJsonAsync<WorkOrderRemarkResponse>() ?? new WorkOrderRemarkResponse();
    }
}
