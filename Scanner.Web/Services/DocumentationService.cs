using System.Net;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Scanner.Web.Services;

public sealed record DocumentationEntry(string Path, string Title, string Section);
public sealed record DocumentationView(string Path, string Title, string Html);

public sealed class DocumentationService
{
    private readonly string root;
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".md"] = "text/markdown; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public DocumentationService(IWebHostEnvironment environment)
    {
        root = System.IO.Path.Combine(environment.ContentRootPath, "Documentation");
        if (!Directory.Exists(root)) root = System.IO.Path.Combine(AppContext.BaseDirectory, "Documentation");
        if (!Directory.Exists(root) && environment.IsDevelopment())
            root = System.IO.Path.GetFullPath(System.IO.Path.Combine(environment.ContentRootPath, "..", "docs"));
    }

    // The allowlist is built from regular files, never from a user-provided filesystem path.
    private Dictionary<string, string> Files()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return files;
        foreach (string file in Directory.EnumerateFiles(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true
        }))
            if (MimeTypes.ContainsKey(System.IO.Path.GetExtension(file)))
                files[System.IO.Path.GetRelativePath(root, file).Replace('\\', '/')] = file;
        return files;
    }

    public IReadOnlyList<DocumentationEntry> List() => Files()
        .Where(item => item.Key.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        .Select(item => new DocumentationEntry(item.Key, Title(item.Value), Section(item.Key)))
        .OrderBy(item => item.Section == "仓库 SOP" ? 0 : item.Section == "SOP (English)" ? 1 : item.Section == "系统资料" ? 2 : item.Section == "数据库资料" ? 3 : 4)
        .ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();

    public (string File, string Mime)? FindFile(string? path)
    {
        if (path is null || !Files().TryGetValue(path, out string? file)) return null;
        return (file, MimeTypes[System.IO.Path.GetExtension(file)]);
    }

    public DocumentationView? Render(string? path)
    {
        var files = Files();
        if (path is null || !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || !files.TryGetValue(path, out string? file)) return null;
        var document = Markdown.Parse(File.ReadAllText(file), pipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            string url = link.Url ?? "";
            link.GetDynamicUrl = null;
            if (url.StartsWith('#')) { link.Url = url; continue; }
            if (Uri.TryCreate(url, UriKind.Absolute, out var external))
            {
                link.Url = !link.IsImage && external.Scheme == "https" ? external.AbsoluteUri : "#";
                continue;
            }
            string? target = ResolveLink(path, url);
            if (target is null || !files.TryGetValue(target, out string? resource)) { link.Url = "#"; continue; }
            if (link.IsImage)
                link.Url = MimeTypes[System.IO.Path.GetExtension(resource)].StartsWith("image/", StringComparison.Ordinal)
                    ? "data:" + MimeTypes[System.IO.Path.GetExtension(resource)] + ";base64," + Convert.ToBase64String(File.ReadAllBytes(resource)) : "#";
            else
                link.Url = target.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    ? "/documents.html?path=" + Uri.EscapeDataString(target)
                    : "/api/documents/file?path=" + Uri.EscapeDataString(target);
        }
        string title = Title(file);
        string language = path.StartsWith("sop-en/", StringComparison.Ordinal) ? "en" : "zh-CN";
        string html = "<!doctype html><html lang=\"" + language + "\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>"
            + WebUtility.HtmlEncode(title) + "</title><style>"
            + "body{font:17px/1.8 system-ui,sans-serif;color:#172b3a;background:#fff;margin:0;padding:20px;overflow-wrap:anywhere}"
            + "h1,h2,h3{line-height:1.4}a{color:#086ca5}img{max-width:100%;height:auto}table{border-collapse:collapse;display:block;overflow-x:auto}"
            + "th,td{border:1px solid #cdd8df;padding:8px 12px;text-align:left}th{background:#edf4f8}pre{overflow-x:auto;background:#edf4f8;padding:12px}"
            + "code{font-size:.9em}blockquote{border-left:3px solid #9bc5da;padding-left:15px;margin-left:0}"
            + "</style></head><body>" + document.ToHtml(pipeline) + "</body></html>";
        return new(path, title, html);
    }

    private static string? ResolveLink(string path, string url)
    {
        if (url.Contains('\\') || url.StartsWith('/') || url.Contains('\0')) return null;
        try
        {
            var baseUri = new Uri("https://documents.invalid/docs/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString)));
            var resolved = new Uri(baseUri, url);
            if (resolved.Host != baseUri.Host) return null;
            string resolvedPath = Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/'));
            if (resolvedPath.StartsWith("docs/", StringComparison.Ordinal)) return resolvedPath[5..];
            return resolvedPath == "request.md" ? resolvedPath : null;
        }
        catch (UriFormatException) { return null; }
    }

    private static string Title(string file) => File.ReadLines(file).FirstOrDefault(line => line.StartsWith("# "))?[2..].Trim()
        ?? System.IO.Path.GetFileNameWithoutExtension(file);
    private static string Section(string path) => path.StartsWith("sop/") ? "仓库 SOP"
        : path.StartsWith("sop-en/") ? "SOP (English)"
        : path.StartsWith("history/") ? "历史归档" : path.StartsWith("database/") ? "数据库资料" : "系统资料";
}
