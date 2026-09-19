namespace Scanner.Web.Services;

public interface IUploadService
{
    Task<UploadResult> SaveAsync(IFormFile file, string deviceName, CancellationToken cancellationToken = default);
    IReadOnlyList<UploadListItem> List();
    Task<UploadPreview> PreviewAsync(string id, CancellationToken cancellationToken = default);
    UploadDownload GetDownload(string id);
}
