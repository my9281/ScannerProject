namespace Scanner.Web.Services;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";
    public string ApiKey { get; set; } = string.Empty;
    public string Directory { get; set; } = "App_Data/uploads";
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxPreviewBytes { get; set; } = 1024 * 1024;
}

public sealed record UploadResult(string FileName, string OriginalName, long Size, DateTimeOffset UploadedAtUtc, string Message);
public sealed record UploadListItem(string Id, string FileName, string Type, long Size, DateTimeOffset UploadedAtUtc);
public sealed record UploadPreview(string FileName, string Type, string Content);
public sealed record UploadDownload(string Path, string FileName);

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
