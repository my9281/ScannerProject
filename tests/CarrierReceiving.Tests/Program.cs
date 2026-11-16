using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scanner.Web.Filters;
using Scanner.Web.Models;
using Scanner.Web.Services;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("Expected invalid input rejection"); }
Check(CarrierTrackingClient.Carrier("FedEx") == "fedex", "carrier normalization");
Reject(() => CarrierTrackingClient.Carrier("https://example.com"));
Reject(() => CarrierTrackingClient.Number("1234/../../secret"));
Reject(() => CarrierTrackingClient.Number("１２３４５６７８９０"));
Check(CarrierTrackingClient.Number(" 123456789012 ") == "123456789012", "trim scanner input");
var fedex = CarrierTrackingClient.Parse("fedex", JsonNode.Parse("""{"output":{"completeTrackResults":[{"trackResults":[{"latestStatusDetail":{"code":"DL","description":"Delivered"}}]}]}}""")!);
Check(fedex.Delivered, "FedEx delivered code");
var usps = CarrierTrackingClient.Parse("usps", JsonNode.Parse("""[{"status":"Delivered, In/At Mailbox","statusCategory":"Delivered"}]""")!);
Check(usps.Delivered, "USPS delivered category");
var pending = CarrierTrackingClient.Parse("usps", JsonNode.Parse("""[{"status":"Awaiting Delivery","statusCategory":"In Transit"}]""")!);
Check(!pending.Delivered, "prose cannot imply delivery");
try { CarrierTrackingClient.Parse("usps", JsonNode.Parse("""[{"error":{"code":"404"}}]""")!); throw new Exception("Expected carrier error"); }
catch (ApiException) { checks++; }
var item = new ReceivingItem(1, "fedex", "123456789012", "", DateTime.UtcNow, null, null, "Delivered", true, DateTime.UtcNow);
Check(item.Reconciliation == "显示送达但未实收", "delivery discrepancy");
Check((item with { ReceivedAt = DateTime.UtcNow }).Reconciliation == "已实收", "physical receipt wins");

foreach (var carrier in new[] { "fedex", "usps" })
{
    var handler = new StubHandler(carrier);
    var client = new CarrierTrackingClient(new HttpClient(handler));
    var result = await client.TrackAsync(carrier, "123456789012", new(true, true, "key", "secret"), default);
    Check(result.Delivered && handler.Calls == 2, carrier + " OAuth and query");
    Check(handler.Hosts.All(h => h == (carrier == "fedex" ? "apis-sandbox.fedex.com" : "apis-tem.usps.com")), "sandbox isolation");
}
foreach (var role in new[] { "", "viewer", "user", "admin" })
{
    using var provider = new ServiceCollection().AddSingleton<IAccountService>(new TestAccounts(role)).BuildServiceProvider();
    var http = new DefaultHttpContext { RequestServices = provider };
    if (role.Length > 0) http.Request.Headers.Authorization = "Bearer test-session";
    var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    await new AccountPermissionAttribute("system.configure").OnAuthorizationAsync(context);
    Check(role switch { "" => context.Result is UnauthorizedObjectResult,
        "admin" => context.Result is null && http.Items[typeof(AccountResult)] is AccountResult { Domain.Id: 7 },
        _ => context.Result is ObjectResult { StatusCode: 403 } }, "configuration access for " + role);
}
foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests })
{
    var client = new CarrierTrackingClient(new HttpClient(new ErrorHandler(code)));
    try { await client.TrackAsync("fedex", "123456789012", new(true, false, "key", "secret"), default); throw new Exception("Expected upstream failure"); }
    catch (ApiException ex) { Check(!ex.Message.Contains("sensitive-provider-body") && ex.StatusCode == 502, "safe upstream error " + code); }
}
checks += await BluettiChecks.Run();
checks += await ReceiptQueueChecks.Run();
foreach (var role in new[] { "viewer", "user", "admin" })
{
    foreach (string permission in new[] { "receiving.read", "receiving.write" })
    {
        using var provider = new ServiceCollection().AddSingleton<IAccountService>(new TestAccounts(role)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider }; http.Request.Headers.Authorization = "Bearer test-session";
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        await new AccountPermissionAttribute(permission).OnAuthorizationAsync(context);
        Check((context.Result is null) == (permission == "receiving.read" || role != "viewer"), role + " " + permission);
    }
}
Console.WriteLine($"Passed {checks} carrier receiving checks.");

sealed class TestAccounts(string role) : IAccountService
{
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult<AccountResult?>(new("tester", null, new AccountDomain(7, "test-domain"), role, token, DateTimeOffset.UtcNow.AddMinutes(1)));
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task LogoutAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class ErrorHandler(HttpStatusCode status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("sensitive-provider-body") });
}

sealed class StubHandler(string carrier) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public List<string> Hosts { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Hosts.Add(request.RequestUri!.Host);
        if (Calls == 1)
        {
            if (request.Method != HttpMethod.Post || request.Headers.Authorization != null) throw new Exception("OAuth request");
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!body.Contains("client_credentials") || !body.Contains("secret")) throw new Exception("OAuth payload");
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"access_token":"test-token"}""") };
        }
        if (request.Headers.Authorization?.ToString() != "Bearer test-token" || request.Method != HttpMethod.Post) throw new Exception("Authenticated tracking request");
        var payload = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
        if (carrier == "usps" && (request.RequestUri.AbsolutePath != "/tracking/v3r2/tracking" || payload[0]?["trackingNumber"]?.ToString() != "123456789012")) throw new Exception("USPS v3r2 contract");
        return new(HttpStatusCode.OK) { Content = new StringContent(carrier == "fedex"
            ? """{"output":{"completeTrackResults":[{"trackResults":[{"latestStatusDetail":{"code":"DL","description":"Delivered"}}]}]}}"""
            : """[{"status":"Delivered","statusCategory":"Delivered"}]""") };
    }
}
