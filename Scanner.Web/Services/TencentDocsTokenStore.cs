using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Scanner.Web.Services;

public sealed record TencentDocsTokens(string ClientId, string AccessToken, string RefreshToken,
    string OpenId, DateTimeOffset ExpiresAt, DateTimeOffset ConnectedAt);

/// <summary>One installation-wide connection; encrypted at rest and never returned by API endpoints.</summary>
public sealed class TencentDocsTokenStore
{
    private readonly string root;
    private readonly IDataProtector protector;

    public TencentDocsTokenStore(IOptions<TencentDocsOptions> options, IWebHostEnvironment environment)
    {
        root = TencentDocsOptions.ResolveStoragePath(options.Value, environment);
        var protection = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(root, "keys")),
            builder => builder.SetApplicationName("Scanner.Web.TencentDocs"));
        protector = protection.CreateProtector("Scanner.Web.TencentDocs.Tokens.v1");
    }

    public async Task<FileStream> AcquireAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        // FileShare.None also serializes refresh/reconnect during IIS overlapped recycling.
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(root, "connection.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 200) { await Task.Delay(50, ct); }
            catch (IOException) { throw new ApiException(503, "腾讯文档连接正忙，请稍后重试。"); }
        }
    }

    public async Task<TencentDocsTokens?> ReadAsync(CancellationToken ct)
    {
        var path = Path.Combine(root, "connection.protected");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<TencentDocsTokens>(protector.Unprotect(await File.ReadAllTextAsync(path, ct)))
                ?? throw new JsonException();
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
        { throw new ApiException(503, "无法解密腾讯文档授权，请恢复服务器 Data Protection 密钥或重新授权。"); }
    }

    public async Task SaveAsync(TencentDocsTokens tokens, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        var pending = Path.Combine(root, Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            await File.WriteAllTextAsync(pending, protector.Protect(JsonSerializer.Serialize(tokens)), ct);
            File.Move(pending, Path.Combine(root, "connection.protected"), overwrite: true);
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
}
