using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Scanner.Web.Services;

public sealed record WpfUpdateManifest(string Version, string PackageUrl, string Sha256);

public sealed class WpfUpdateService
{
    public const long MaxPackageBytes = 512L * 1024 * 1024;
    public const long MaxRequestBytes = MaxPackageBytes + 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _root;

    public WpfUpdateService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        string configured = configuration["WpfUpdates:StoragePath"] ?? "App_Data/WpfUpdates";
        _root = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured));
    }

    public async Task<WpfUpdateManifest?> GetLatestAsync(CancellationToken token = default)
    {
        string path = Path.Combine(_root, "latest.json");
        if (!File.Exists(path)) return null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return await JsonSerializer.DeserializeAsync<WpfUpdateManifest>(stream, JsonOptions, token);
    }

    public string? GetPackagePath(string name)
    {
        if (name != Path.GetFileName(name) || name.Contains('\\') || name.Contains(':') ||
            !System.Text.RegularExpressions.Regex.IsMatch(name, @"\AScanner\.WPF-\d+\.\d+\.\d+\.\d+-[a-f0-9]{64}-win-x64\.zip\z")) return null;
        string path = Path.Combine(_root, "packages", name);
        return File.Exists(path) ? path : null;
    }

    public async Task<WpfUpdateManifest> PublishAsync(IFormFile file, CancellationToken token)
    {
        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || file.Length <= 0)
            throw new ApiException(400, "请选择发布脚本生成的 WPF ZIP 更新包。");
        if (file.Length > MaxPackageBytes) throw new ApiException(413, "更新包不能超过 512 MB。");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "packages"));
        string pending = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".upload");
        string pendingManifest = pending + ".json";
        try
        {
            await using (var input = file.OpenReadStream())
            await using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > MaxPackageBytes) throw new ApiException(413, "更新包不能超过 512 MB。");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                }
            }
            Version version;
            try { version = ValidatePackage(pending); }
            catch (Exception ex) when (ex is InvalidDataException or BadImageFormatException or JsonException or ArgumentException or InvalidOperationException)
            { throw new ApiException(400, "更新包无效：" + ex.Message); }
            string hash;
            await using (var stream = File.OpenRead(pending))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();

            // Exclusive file lease also serializes publication across IIS worker processes.
            await using var lease = await AcquirePublishLeaseAsync(token);
            WpfUpdateManifest? current = await GetLatestAsync(token);
            if (current != null && version <= Version.Parse(current.Version))
                throw new ApiException(409, $"上传版本 {version} 必须高于当前版本 {current.Version}，请先提高 WPF 程序版本号。");
            string name = $"Scanner.WPF-{version}-{hash}-win-x64.zip";
            string packagePath = Path.Combine(_root, "packages", name);
            var manifest = new WpfUpdateManifest(version.ToString(), "/updates/wpf/packages/" + name, hash);
            await File.WriteAllTextAsync(pendingManifest, JsonSerializer.Serialize(manifest, JsonOptions), token);
            // Make the immutable package available first; only then replace the manifest atomically.
            File.Move(pending, packagePath, overwrite: true);
            File.Move(pendingManifest, Path.Combine(_root, "latest.json"), overwrite: true);
            return manifest;
        }
        finally
        {
            if (File.Exists(pending)) File.Delete(pending);
            if (File.Exists(pendingManifest)) File.Delete(pendingManifest);
        }
    }

    private async Task<FileStream> AcquirePublishLeaseAsync(CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(_root, "publish.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 100) { await Task.Delay(100, token); }
        }
    }

    private static Version ValidatePackage(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 10000) throw new InvalidDataException("ZIP 文件数量过多。");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(part => part == ".." || part.EndsWith('.') || part.EndsWith(' ')) ||
                !paths.Add(name)) throw new InvalidDataException("ZIP 包含不安全或重复路径。");
            if (entry.Length > 1024L * 1024 * 1024 - total) throw new InvalidDataException("ZIP 解压体积超过 1 GB。");
            total += entry.Length;
        }
        foreach (string name in new[] { "Scanner.WPF.exe", "Scanner.WPF.dll", "Scanner.WPF.deps.json", "Scanner.WPF.runtimeconfig.json" })
            if (zip.GetEntry(name) == null) throw new InvalidDataException($"ZIP 根目录缺少 {name}，请上传发布脚本生成的包。");
        using (var exe = new PEReader(new MemoryStream(ReadEntry(zip.GetEntry("Scanner.WPF.exe")!, 16 * 1024 * 1024))))
            if (exe.PEHeaders.CoffHeader.Machine != Machine.Amd64) throw new InvalidDataException("只接受 win-x64 发布包。");
        using var pe = new PEReader(new MemoryStream(ReadEntry(zip.GetEntry("Scanner.WPF.dll")!, 256 * 1024 * 1024)));
        var metadata = pe.GetMetadataReader();
        var assembly = metadata.GetAssemblyDefinition();
        if (metadata.GetString(assembly.Name) != "Scanner.WPF") throw new InvalidDataException("DLL 不是 Scanner.WPF 程序。");
        using var runtime = JsonDocument.Parse(ReadEntry(zip.GetEntry("Scanner.WPF.runtimeconfig.json")!, 64 * 1024));
        if (!runtime.RootElement.TryGetProperty("runtimeOptions", out var options) ||
            !options.TryGetProperty("tfm", out var tfm) || tfm.GetString() != "net10.0")
            throw new InvalidDataException("更新包必须使用 .NET 10。");
        return assembly.Version;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int limit)
    {
        if (entry.Length > limit) throw new InvalidDataException("ZIP 内容超过校验体积限制。");
        using var input = entry.Open();
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int count;
        while ((count = input.Read(bytes)) > 0)
        {
            if (buffer.Length + count > limit) throw new InvalidDataException("ZIP 内容超过校验体积限制。");
            buffer.Write(bytes, 0, count);
        }
        return buffer.ToArray();
    }
}
