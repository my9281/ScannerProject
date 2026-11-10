using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Scanner.Web.Services;

public sealed class UploadService(IOptions<UploadOptions> options, IWebHostEnvironment environment) : IUploadService
{
    private readonly UploadOptions _options = options.Value;
    private readonly string _root = ResolveRoot(options.Value.Directory, environment.ContentRootPath);

    public async Task<UploadResult> SaveAsync(IFormFile file, string deviceName, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0) throw new ApiException(StatusCodes.Status400BadRequest, "没有收到文件。");
        if (file.Length > _options.MaxBytes) throw new ApiException(StatusCodes.Status413PayloadTooLarge, $"文件不能超过 {FormatSize(_options.MaxBytes)}。");
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".txt" and not ".json") throw new ApiException(StatusCodes.Status400BadRequest, "只接受 TXT 或 JSON 文件。");
        if (extension == ".json") await ValidateJsonAsync(file, cancellationToken);

        DateTime utcNow = DateTime.UtcNow;
        string dateFolder = Path.Combine(_root, utcNow.ToString("yyyyMMdd"));
        Directory.CreateDirectory(dateFolder);
        string storedName = $"{utcNow:yyyyMMdd_HHmmssfff}_{Sanitize(deviceName, 40)}_{Sanitize(Path.GetFileNameWithoutExtension(file.FileName), 80)}_{Guid.NewGuid():N}{extension}";
        string destination = Path.Combine(dateFolder, storedName);
        await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await file.CopyToAsync(output, cancellationToken);
        return new(storedName, file.FileName, file.Length, new DateTimeOffset(utcNow, TimeSpan.Zero), "上传成功。");
    }

    public IReadOnlyList<UploadListItem> List()
    {
        if (!Directory.Exists(_root)) return Array.Empty<UploadListItem>();
        return Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Where(IsSupported).Select(path =>
        {
            FileInfo info = new(path);
            return new UploadListItem(EncodeId(Path.GetRelativePath(_root, path)), info.Name, info.Extension.TrimStart('.').ToUpperInvariant(), info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
        }).OrderByDescending(file => file.UploadedAtUtc).ToArray();
    }

    public async Task<UploadPreview> PreviewAsync(string id, CancellationToken cancellationToken = default)
    {
        string path = ResolveFile(id);
        FileInfo info = new(path);
        if (info.Length > _options.MaxPreviewBytes) throw new ApiException(StatusCodes.Status413PayloadTooLarge, $"文件超过 {FormatSize(_options.MaxPreviewBytes)}，请下载后查看。");
        return new(info.Name, info.Extension.TrimStart('.').ToLowerInvariant(), await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken));
    }

    public UploadDownload GetDownload(string id)
    {
        string path = ResolveFile(id);
        return new(path, Path.GetFileName(path));
    }

    private string ResolveFile(string id)
    {
        try
        {
            string path = Path.GetFullPath(Path.Combine(_root, DecodeId(id)));
            string rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(path) && IsSupported(path)) return path;
        }
        catch (FormatException) { }
        throw new ApiException(StatusCodes.Status404NotFound, "文件不存在。");
    }

    private static async Task ValidateJsonAsync(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            await using Stream stream = file.OpenReadStream();
            using JsonDocument _ = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException) { throw new ApiException(StatusCodes.Status400BadRequest, "JSON 文件格式无效，请检查后重试。"); }
    }

    private static string ResolveRoot(string configured, string contentRoot)
    {
        string root = string.IsNullOrWhiteSpace(configured) ? Path.Combine("App_Data", "uploads") : configured;
        return Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(contentRoot, root));
    }

    private static bool IsSupported(string path) => Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".json";
    private static string EncodeId(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string DecodeId(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private static string Sanitize(string value, int maxLength)
    {
        string source = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = new(source.Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character).Take(maxLength).ToArray());
        return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
    }

    private static string FormatSize(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.#} MB" : $"{bytes / 1024d:0.#} KB";
}
