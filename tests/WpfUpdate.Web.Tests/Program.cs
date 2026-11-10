using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner.Web.Controllers;
using Scanner.Web.Filters;
using Scanner.Web.Models;
using Scanner.Web.Services;

if (args.Length != 1) throw new ArgumentException("Provide a published WPF ZIP path.");
string packagePath = Path.GetFullPath(args[0]);
string root = Path.Combine(Path.GetTempPath(), "ScannerWpfWebTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Development" });
builder.Logging.ClearProviders();
builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>()).AddApplicationPart(typeof(WpfUpdatesController).Assembly);
builder.Services.AddSingleton<WpfUpdateService>();
builder.Services.AddSingleton<IAccountService, FakeAccounts>();
await using var app = builder.Build();
app.Urls.Add("http://127.0.0.1:0");
app.MapControllers();
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
try
{
    await app.StartAsync();
    string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    var latest = await client.GetAsync("/updates/wpf/latest.json");
    Check(latest.StatusCode == HttpStatusCode.NotFound, "Empty release is 404");
    Check(latest.Headers.CacheControl?.NoStore == true, "Manifest is not cached");
    foreach (var (token, expected) in new[] { ("", HttpStatusCode.Unauthorized), ("user", HttpStatusCode.Forbidden), ("viewer", HttpStatusCode.Forbidden) })
    {
        using var response = await Upload(client, token, new MemoryStream([1, 2, 3]), "test.zip");
        Check(response.StatusCode == expected, "Publishing requires an administrator: " + token);
    }
    using (var response = await Upload(client, "admin", new MemoryStream([1, 2, 3]), "test.zip"))
        Check(response.StatusCode == HttpStatusCode.BadRequest, "Malformed ZIP is rejected");
    var unsafeZip = new MemoryStream();
    using (var zip = new ZipArchive(unsafeZip, ZipArchiveMode.Create, true)) zip.CreateEntry("../escape.dll");
    unsafeZip.Position = 0;
    using (var response = await Upload(client, "admin", unsafeZip, "unsafe.zip"))
        Check(response.StatusCode == HttpStatusCode.BadRequest, "ZIP traversal is rejected");
    using (var response = await Upload(client, "admin", File.OpenRead(packagePath), "release.zip"))
        Check(response.IsSuccessStatusCode, "Admin can publish a real WPF ZIP: " + await response.Content.ReadAsStringAsync());
    latest = await client.GetAsync("/updates/wpf/latest.json");
    var manifest = await latest.Content.ReadFromJsonAsync<WpfUpdateManifest>();
    Check(latest.IsSuccessStatusCode && manifest != null, "Published manifest is public");
    Check(manifest!.PackageUrl.StartsWith("/updates/wpf/packages/"), "Package URL uses current site");
    await using (var file = File.OpenRead(packagePath))
        Check(manifest.Sha256 == Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant(), "Manifest checksum matches uploaded bytes");
    using (var download = await client.GetAsync(manifest.PackageUrl))
    {
        Check(download.IsSuccessStatusCode && download.Content.Headers.ContentType?.MediaType == "application/zip", "Public package download");
        Check(download.Headers.CacheControl?.Extensions.Any(value => value.Name == "immutable") == true, "Versioned package is immutable");
        await using var stream = await download.Content.ReadAsStreamAsync();
        Check(Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant() == manifest.Sha256, "Downloaded ZIP is identical");
    }
    using (var request = new HttpRequestMessage(HttpMethod.Get, manifest.PackageUrl))
    {
        request.Headers.Range = new RangeHeaderValue(0, 99);
        using var response = await client.SendAsync(request);
        Check(response.StatusCode == HttpStatusCode.PartialContent, "Package supports range downloads");
    }
    using (var response = await Upload(client, "admin", File.OpenRead(packagePath), "duplicate.zip"))
        Check(response.StatusCode == HttpStatusCode.Conflict, "Same version cannot overwrite a release");
    var service = app.Services.GetRequiredService<WpfUpdateService>();
    Check(service.GetPackagePath("../latest.json") == null && service.GetPackagePath("bad.zip") == null, "Download paths cannot escape storage");
    Check((await service.GetLatestAsync()) == manifest, "Rejected publication retains current manifest");
    Check(!Directory.GetFiles(Path.Combine(root, "App_Data", "WpfUpdates"), "*.upload*").Any(), "Temporary uploads cleaned up");
    // Simulate a newer deployed release: uploading the earlier package must be rejected.
    string storedManifest = Path.Combine(root, "App_Data", "WpfUpdates", "latest.json");
    await File.WriteAllTextAsync(storedManifest, System.Text.Json.JsonSerializer.Serialize(manifest with { Version = "99.0.0.0" }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    using (var response = await Upload(client, "admin", File.OpenRead(packagePath), "older.zip"))
        Check(response.StatusCode == HttpStatusCode.Conflict, "Older version is rejected");
    File.Delete(storedManifest);
    var concurrent = await Task.WhenAll(Upload(client, "admin", File.OpenRead(packagePath), "one.zip"), Upload(client, "admin", File.OpenRead(packagePath), "two.zip"));
    Check(concurrent.Count(value => value.IsSuccessStatusCode) == 1 && concurrent.Count(value => value.StatusCode == HttpStatusCode.Conflict) == 1,
        "Concurrent uploads publish exactly once");
    foreach (var response in concurrent) response.Dispose();
    Console.WriteLine($"PASS: {checks} HTTP upload, permission, validation, manifest, download and downgrade checks.");
}
finally { await app.StopAsync(); Directory.Delete(root, true); }

static async Task<HttpResponseMessage> Upload(HttpClient client, string token, Stream stream, string name)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/updates/wpf");
    if (token.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var form = new MultipartFormDataContent();
    form.Add(new StreamContent(stream), "file", name);
    request.Content = form;
    return await client.SendAsync(request);
}

sealed class FakeAccounts : IAccountService
{
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult<AccountResult?>(
        token is "admin" or "user" or "viewer" ? new(token, token, new AccountDomain(1, "test"), token, token, DateTimeOffset.UtcNow.AddHours(1)) : null);
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task LogoutAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
