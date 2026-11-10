namespace Scanner.Web.Services;

public sealed class TencentDocsOptions
{
    public const string SectionName = "TencentDocs";
    public bool Enabled { get; set; }
    public string AuthMode { get; set; } = "OAuth";
    public string AccessToken { get; set; } = "";
    public string OpenId { get; set; } = "";
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }
    public bool SyncEnabled { get; set; }
    public bool SyncAutoPush { get; set; } = true;
    public int SyncIntervalSeconds { get; set; } = 300;
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string StoragePath { get; set; } = "App_Data/TencentDocs";
    public string EncodedId { get; set; } = "DYk5kdUx1aGJUQXdp";
    public string FileId { get; set; } = "";
    public string SheetId { get; set; } = "t00i2h";

    public bool UsesDirectToken => string.Equals(AuthMode, "DirectToken", StringComparison.OrdinalIgnoreCase);
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ClientId) && (UsesDirectToken
        ? !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(OpenId)
        : string.Equals(AuthMode, "OAuth", StringComparison.OrdinalIgnoreCase) && IsOAuthConfigured);

    public bool IsOAuthConfigured => !string.IsNullOrWhiteSpace(ClientSecret)
        && Uri.TryCreate(RedirectUri, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && uri.AbsolutePath == "/api/tencent-docs/oauth/callback" && string.IsNullOrEmpty(uri.UserInfo);

    public static string ResolveStoragePath(TencentDocsOptions options, IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(Path.IsPathRooted(options.StoragePath) ? options.StoragePath
            : Path.Combine(environment.ContentRootPath, options.StoragePath));
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var relative = Path.GetRelativePath(webRoot, root);
        if (relative == "." || !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("TencentDocs:StoragePath 不能放在 wwwroot 中。");
        return root;
    }
}
