using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController, Route("api/carrier-receiving"), AccountPermission("system.configure")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CarrierReceivingController(CarrierReceivingStore store, CarrierTrackingClient tracking) : ControllerBase
{
    private AccountResult Account => (AccountResult)HttpContext.Items[typeof(AccountResult)]!;

    [HttpGet("settings/{carrier}")]
    public async Task<IActionResult> Settings(string carrier, CancellationToken ct) =>
        Ok(await store.Settings(Account.Domain.Id, CarrierTrackingClient.Carrier(carrier), ct));

    [HttpPut("settings/{carrier}"), RequestSizeLimit(16384)]
    public async Task<IActionResult> SaveSettings(string carrier, CarrierSettingsEdit edit, CancellationToken ct)
    {
        await store.SaveSettings(Account.Domain.Id, CarrierTrackingClient.Carrier(carrier), Account.Username, edit, ct);
        return Ok(new { message = "已保存，下一次查询立即生效。" });
    }

    [HttpGet("items")]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] ulong before = 0, [FromQuery] string search = "") =>
        Ok(await store.List(Account.Domain.Id, before, search, ct));

    public sealed record ScanRequest(string Carrier, string TrackingNumber, string Reference = "", bool Receive = false);
    [HttpPost("items"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Register(ScanRequest scan, CancellationToken ct)
    {
        await store.Register(Account.Domain.Id, CarrierTrackingClient.Carrier(scan.Carrier), CarrierTrackingClient.Number(scan.TrackingNumber),
            scan.Reference ?? "", Account.Username, scan.Receive, ct);
        return Ok(new { message = scan.Receive ? "已登记实收（重复扫描保留首次收货时间）。" : "已登记预报运单。" });
    }

    [HttpPost("track"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Track(ScanRequest scan, CancellationToken ct)
    {
        string carrier = CarrierTrackingClient.Carrier(scan.Carrier), number = CarrierTrackingClient.Number(scan.TrackingNumber);
        var config = await store.Credentials(Account.Domain.Id, carrier, ct);
        var result = await tracking.TrackAsync(carrier, number, config, ct);
        if (!await store.UpdateTracking(Account.Domain.Id, carrier, number, result, ct))
            throw new ApiException(404, "请先登记该运单，再查询物流。");
        return Ok(result);
    }
}
