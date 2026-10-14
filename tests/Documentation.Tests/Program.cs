using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scanner.Web.Controllers;
using Scanner.Web.Models;
using Scanner.Web.Services;

string temporary = Path.Combine(Path.GetTempPath(), "scanner-documents-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(temporary, "Documentation", "sop", "images"));
string docs = Path.Combine(temporary, "Documentation");
File.WriteAllText(Path.Combine(docs, "sop", "example.md"), """
# 测试 SOP

| 字段 | 内容 |
| --- | --- |
| SN | 设备编号 |

![截图](images/example.png)
[下一篇](next.md)
[附件](reference.xlsx)
[外部](https://example.com/)
[危险](javascript:alert%281%29)
<script>alert(1)</script>
![外部图片](https://example.com/tracking.png)
""");
File.WriteAllText(Path.Combine(docs, "sop", "next.md"), "# 下一篇\n\n内容");
File.WriteAllBytes(Path.Combine(docs, "sop", "images", "example.png"), [137, 80, 78, 71]);
File.WriteAllBytes(Path.Combine(docs, "sop", "reference.xlsx"), [1, 2, 3]);
File.WriteAllText(Path.Combine(docs, "secret.json"), "secret");
File.WriteAllText(Path.Combine(temporary, "outside.md"), "outside secret");
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
bool preview = args.Contains("--preview");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = temporary, EnvironmentName = Environments.Production,
    WebRootPath = preview ? Path.GetFullPath("Scanner.Web/wwwroot") : null
});
builder.Logging.ClearProviders();
builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
builder.Services.AddControllers().AddApplicationPart(typeof(DocumentsController).Assembly);
builder.Services.AddSingleton<DocumentationService>();
builder.Services.AddSingleton<IAccountService, Accounts>();
await using var app = builder.Build();
if (preview) { app.UseDefaultFiles(); app.UseStaticFiles(); }
app.MapControllers();
try
{
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    foreach (string endpoint in new[] { "api/documents", "api/documents/render?path=sop/example.md", "api/documents/file?path=sop/reference.xlsx" })
        Check((await client.GetAsync(endpoint)).StatusCode == HttpStatusCode.Unauthorized, "Anonymous document/resource access denied");
    foreach (string role in new[] { "viewer", "user", "admin" })
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", role);
        var entries = await client.GetFromJsonAsync<DocumentationEntry[]>("api/documents");
        Check(entries is { Length: 2 } && entries.All(entry => entry.Section == "仓库 SOP"), "Read-only and editor roles can list documents");
    }
    var rendered = await client.GetFromJsonAsync<DocumentationView>("api/documents/render?path=sop/example.md");
    Check(rendered is not null && rendered.Html.Contains("<table>"), "Markdown table rendered");
    Check(rendered!.Html.Contains("data:image/png;base64,"), "Protected image embedded without public URL");
    Check(rendered.Html.Contains("/documents.html?path=sop%2Fnext.md"), "Relative document link stays in library");
    Check(rendered.Html.Contains("/api/documents/file?path=sop%2Freference.xlsx"), "Attachment link uses protected API");
    Check(!rendered.Html.Contains("<script>") && !rendered.Html.Contains("href=\"javascript:"), "Raw HTML and active URL schemes blocked");
    Check(!rendered.Html.Contains("https://example.com/tracking.png"), "External images cannot track readers");
    foreach (string path in new[] { "../outside.md", "sop/../../outside.md", "/outside.md", "secret.json", "sop\\example.md", "sop/images/example.png" })
        Check((await client.GetAsync("api/documents/render?path=" + Uri.EscapeDataString(path))).StatusCode == HttpStatusCode.NotFound, "Render rejects traversal, disallowed files and non-Markdown");
    foreach (string path in new[] { "../outside.md", "secret.json" })
        Check((await client.GetAsync("api/documents/file?path=" + Uri.EscapeDataString(path))).StatusCode == HttpStatusCode.NotFound, "Downloads remain in file allowlist");
    using var download = await client.GetAsync("api/documents/file?path=sop/reference.xlsx");
    Check((await download.Content.ReadAsByteArrayAsync()).SequenceEqual(new byte[] { 1, 2, 3 }), "Attachment bytes unchanged");
    Check(download.Content.Headers.ContentDisposition?.DispositionType == "attachment", "Attachment download disposition");
    Check(download.Headers.CacheControl?.NoStore == true, "Protected documents not cached");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "expired");
    Check((await client.GetAsync("api/documents")).StatusCode == HttpStatusCode.Unauthorized, "Expired session denied");
    string repoDocs = Path.GetFullPath("docs");
    if (Directory.Exists(repoDocs))
    {
        foreach (string source in Directory.EnumerateFiles(repoDocs, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(docs, Path.GetRelativePath(repoDocs, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
        }
        File.Copy("request.md", Path.Combine(docs, "request.md"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "viewer");
        var library = await client.GetFromJsonAsync<DocumentationEntry[]>("api/documents");
        foreach (var item in library!)
            Check((await client.GetAsync("api/documents/render?path=" + Uri.EscapeDataString(item.Path))).IsSuccessStatusCode, "Actual repository document renders: " + item.Path);
        var sop = await client.GetFromJsonAsync<DocumentationView>("api/documents/render?path=" + Uri.EscapeDataString("sop/退回返修SOP.md"));
        var imageReferences = System.Text.RegularExpressions.Regex.Matches(
            File.ReadAllText(Path.Combine(repoDocs, "sop", "退回返修SOP.md")), @"!\[[^\]]*\]\(([^)]+)\)");
        Check(imageReferences.Count > 0, "Actual repair SOP contains reference images");
        foreach (System.Text.RegularExpressions.Match imageReference in imageReferences)
        {
            string imageFile = Path.GetFullPath(Path.Combine(repoDocs, "sop", imageReference.Groups[1].Value));
            Check(File.Exists(imageFile) && sop!.Html.Contains(Convert.ToBase64String(File.ReadAllBytes(imageFile))),
                "Actual referenced image displayed: " + imageReference.Groups[1].Value);
        }
        var receiving = await client.GetFromJsonAsync<DocumentationView>("api/documents/render?path=" + Uri.EscapeDataString("sop/收货SOP.md"));
        Check(receiving!.Html.Contains("/documents.html?path=request.md"), "Root-level functional guide link works");
    }
    Console.WriteLine($"PASS: {checks} documentation integration checks.");
    if (preview)
    {
        Console.WriteLine("PREVIEW: " + app.Urls.Single());
        await app.WaitForShutdownAsync();
    }
}
finally
{
    await app.StopAsync();
    if (!Path.GetFullPath(temporary).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Cleanup path is outside temporary directory.");
    foreach (string file in Directory.EnumerateFiles(temporary, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
    Directory.Delete(temporary, true);
}

sealed class Accounts : IAccountService
{
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult<AccountResult?>(
        token is "viewer" or "user" or "admin" ? new("reader", null, new AccountDomain(1, "test"), token, token, DateTimeOffset.UtcNow.AddHours(1)) : null);
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => GetCurrentAsync("viewer", cancellationToken);
    public Task LogoutAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
