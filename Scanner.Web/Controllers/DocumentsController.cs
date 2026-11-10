using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/documents")]
[AccountPermission("checklist.read")]
public sealed class DocumentsController(DocumentationService documents) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(documents.List());
    }

    [HttpGet("render")]
    public IActionResult Render([FromQuery] string path)
    {
        Response.Headers.CacheControl = "no-store";
        var result = documents.Render(path);
        return result is null ? NotFound(new { message = "文档不存在或不可访问。" }) : Ok(result);
    }

    [HttpGet("file")]
    public IActionResult Download([FromQuery] string path)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var file = documents.FindFile(path);
        return file is null ? NotFound(new { message = "附件不存在或不可访问。" })
            : PhysicalFile(file.Value.File, file.Value.Mime, System.IO.Path.GetFileName(file.Value.File));
    }
}
