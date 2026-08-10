using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
WebApplication app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));

app.MapPost("/api/uploads/scans", async (HttpRequest request, IConfiguration configuration, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    IResult? authenticationError = Authenticate(request, configuration);
    if (authenticationError is not null) return authenticationError;
    if (!request.HasFormContentType) return Results.BadRequest(new { message = "请求必须使用 multipart/form-data。" });

    IFormCollection form = await request.ReadFormAsync(cancellationToken);
    IFormFile? file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest(new { message = "没有收到文件。" });

    long maxBytes = configuration.GetValue("Upload:MaxBytes", 10 * 1024 * 1024L);
    if (file.Length > maxBytes) return Results.Json(new { message = $"文件不能超过 {FormatSize(maxBytes)}。" }, statusCode: StatusCodes.Status413PayloadTooLarge);

    string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (extension is not ".txt" and not ".json") return Results.BadRequest(new { message = "只接受 TXT 或 JSON 文件。" });

    if (extension == ".json")
    {
        try
        {
            await using Stream jsonStream = file.OpenReadStream();
            using JsonDocument _ = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "JSON 文件格式无效，请检查后重试。" });
        }
    }

    string uploadRoot = GetUploadRoot(configuration, environment);
    string dateFolder = Path.Combine(uploadRoot, DateTime.UtcNow.ToString("yyyyMMdd"));
    Directory.CreateDirectory(dateFolder);
    string originalName = Sanitize(Path.GetFileNameWithoutExtension(file.FileName), 80);
    string deviceName = Sanitize(form["deviceName"].ToString(), 40);
    string storedName = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{deviceName}_{originalName}_{Guid.NewGuid():N}{extension}";
    string destination = Path.Combine(dateFolder, storedName);

    await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
    await file.CopyToAsync(output, cancellationToken);

    return Results.Ok(new { fileName = storedName, originalName = file.FileName, size = file.Length, uploadedAtUtc = DateTimeOffset.UtcNow, message = "上传成功。" });
}).DisableAntiforgery();

app.MapGet("/api/uploads", (HttpRequest request, IConfiguration configuration, IWebHostEnvironment environment) =>
{
    IResult? authenticationError = Authenticate(request, configuration);
    if (authenticationError is not null) return authenticationError;

    string uploadRoot = GetUploadRoot(configuration, environment);
    if (!Directory.Exists(uploadRoot)) return Results.Ok(Array.Empty<object>());

    var files = Directory.EnumerateFiles(uploadRoot, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".json")
        .Select(path =>
        {
            FileInfo info = new(path);
            string relativePath = Path.GetRelativePath(uploadRoot, path);
            return new
            {
                id = EncodeId(relativePath),
                fileName = info.Name,
                type = info.Extension.TrimStart('.').ToUpperInvariant(),
                size = info.Length,
                uploadedAtUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)
            };
        })
        .OrderByDescending(file => file.uploadedAtUtc)
        .ToArray();
    return Results.Ok(files);
});

app.MapGet("/api/uploads/{id}/content", async (string id, HttpRequest request, IConfiguration configuration, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    IResult? authenticationError = Authenticate(request, configuration);
    if (authenticationError is not null) return authenticationError;
    string? path = ResolveFile(id, configuration, environment);
    if (path is null) return Results.NotFound(new { message = "文件不存在。" });

    long maxPreviewBytes = configuration.GetValue("Upload:MaxPreviewBytes", 1024 * 1024L);
    FileInfo info = new(path);
    if (info.Length > maxPreviewBytes) return Results.Json(new { message = $"文件超过 {FormatSize(maxPreviewBytes)}，请下载后查看。" }, statusCode: StatusCodes.Status413PayloadTooLarge);

    string content = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
    return Results.Ok(new { fileName = info.Name, type = info.Extension.TrimStart('.').ToLowerInvariant(), content });
});

app.MapGet("/api/uploads/{id}/download", (string id, HttpRequest request, IConfiguration configuration, IWebHostEnvironment environment) =>
{
    IResult? authenticationError = Authenticate(request, configuration);
    if (authenticationError is not null) return authenticationError;
    string? path = ResolveFile(id, configuration, environment);
    return path is null
        ? Results.NotFound(new { message = "文件不存在。" })
        : Results.File(path, "application/octet-stream", Path.GetFileName(path));
});

app.Run();

static IResult? Authenticate(HttpRequest request, IConfiguration configuration)
{
    string configuredKey = configuration["Upload:ApiKey"] ?? string.Empty;
    string suppliedKey = request.Headers["X-Upload-Key"].ToString();
    return !string.IsNullOrWhiteSpace(configuredKey) && !KeysEqual(configuredKey, suppliedKey)
        ? Results.Json(new { message = "访问密钥无效。" }, statusCode: StatusCodes.Status401Unauthorized)
        : null;
}

static string GetUploadRoot(IConfiguration configuration, IWebHostEnvironment environment)
{
    string root = configuration["Upload:Directory"] ?? Path.Combine("App_Data", "uploads");
    if (!Path.IsPathRooted(root)) root = Path.Combine(environment.ContentRootPath, root);
    return Path.GetFullPath(root);
}

static string? ResolveFile(string id, IConfiguration configuration, IWebHostEnvironment environment)
{
    try
    {
        string relativePath = DecodeId(id);
        string root = GetUploadRoot(configuration, environment);
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(path) && Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".json" ? path : null;
    }
    catch (FormatException) { return null; }
}

static string EncodeId(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
static string DecodeId(string value)
{
    string base64 = value.Replace('-', '+').Replace('_', '/');
    base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
    return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
}

static bool KeysEqual(string expected, string actual)
{
    byte[] left = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
    byte[] right = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
    return CryptographicOperations.FixedTimeEquals(left, right);
}

static string Sanitize(string value, int maxLength)
{
    string source = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
    char[] invalid = Path.GetInvalidFileNameChars();
    string result = new(source.Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character).Take(maxLength).ToArray());
    return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
}

static string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.#} MB" : $"{bytes / 1024d:0.#} KB";
