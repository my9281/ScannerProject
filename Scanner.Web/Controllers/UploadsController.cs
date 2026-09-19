using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/uploads")]
[ApiKey]
public sealed class UploadsController(IUploadService uploads) : ControllerBase
{
    [HttpPost("scans")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<ActionResult<UploadResult>> Upload([FromForm] IFormFile file, [FromForm] string? deviceName, CancellationToken cancellationToken)
        => Ok(await uploads.SaveAsync(file, deviceName ?? "unknown", cancellationToken));

    [HttpGet]
    public ActionResult<IReadOnlyList<UploadListItem>> List() => Ok(uploads.List());

    [HttpGet("{id}/content")]
    public async Task<ActionResult<UploadPreview>> Preview(string id, CancellationToken cancellationToken)
        => Ok(await uploads.PreviewAsync(id, cancellationToken));

    [HttpGet("{id}/download")]
    public IActionResult Download(string id)
    {
        UploadDownload file = uploads.GetDownload(id);
        return PhysicalFile(file.Path, "application/octet-stream", file.FileName);
    }
}
