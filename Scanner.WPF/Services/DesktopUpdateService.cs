using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner.WPF.Services
{
    internal static class DesktopUpdateService
    {
        private const long MaxPackageBytes = 512L * 1024 * 1024;
        private const long MaxExtractedBytes = 1024L * 1024 * 1024;

        internal static Version LocalVersion => typeof(DesktopUpdateService).Assembly.GetName().Version;

        public static async Task<bool> TryUpdateAsync(string[] args)
        {
            if (args.Contains("--updated") ||
                bool.TryParse(ConfigurationManager.AppSettings["AutoUpdateEnabled"], out bool enabled) && !enabled)
                return false;

            string stage = null;
            try
            {
                var manifestUri = new Uri(ConfigurationManager.AppSettings["AutoUpdateManifestUrl"]
                    ?? "https://wms.ymforever.com/updates/wpf/latest.json");
                RequireTrustedUri(manifestUri, manifestUri);
                using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
                using var response = await http.GetAsync(manifestUri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                using var manifestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var manifestStream = await response.Content.ReadAsStreamAsync(manifestTimeout.Token);
                using var manifestBuffer = new MemoryStream();
                await CopyBoundedAsync(manifestStream, manifestBuffer, 64 * 1024, manifestTimeout.Token);
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestBuffer.ToArray(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (manifest == null || !Version.TryParse(manifest.Version, out var remoteVersion))
                    throw new InvalidDataException("Invalid update version.");
                Log($"Local={LocalVersion}; remote={remoteVersion}; equal={LocalVersion == remoteVersion}");
                // Never downgrade a newer locally installed build.
                if (remoteVersion <= LocalVersion) return false;
                var packageUri = new Uri(manifestUri, manifest.PackageUrl);
                RequireTrustedUri(packageUri, manifestUri);
                if (manifest.Sha256 == null || manifest.Sha256.Length != 64 || !manifest.Sha256.All(Uri.IsHexDigit))
                    throw new InvalidDataException("Missing or invalid SHA-256.");
                string executable = Environment.ProcessPath;
                if (!string.Equals(Path.GetFileName(executable), "Scanner.WPF.exe", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Updates require the published Scanner.WPF.exe app host.");

                stage = Path.Combine(Path.GetTempPath(), "Scanner.WPF.Update", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                string zip = Path.Combine(stage, "package.zip");
                using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                using (var packageResponse = await http.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, downloadTimeout.Token))
                {
                    packageResponse.EnsureSuccessStatusCode();
                    using var input = await packageResponse.Content.ReadAsStreamAsync(downloadTimeout.Token);
                    using var output = File.Create(zip);
                    await CopyBoundedAsync(input, output, MaxPackageBytes, downloadTimeout.Token);
                }
                using (var file = File.OpenRead(zip))
                {
                    string hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
                    if (!hash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Update checksum mismatch.");
                }
                string payload = Path.Combine(stage, "payload");
                ExtractPackage(zip, payload, remoteVersion);
                string script = Path.Combine(stage, "ApplyUpdate.ps1");
                using (var resource = typeof(DesktopUpdateService).Assembly.GetManifestResourceStream("Scanner.WPF.ApplyUpdate.ps1"))
                using (var output = File.Create(script))
                    await resource.CopyToAsync(output);
                string job = Path.Combine(stage, "job.json");
                await File.WriteAllTextAsync(job, JsonSerializer.Serialize(new
                {
                    ProcessId = Environment.ProcessId,
                    Target = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    Payload = payload,
                    Executable = executable,
                    Restart = true
                }));
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe"))
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-JobPath", job })
                    start.ArgumentList.Add(arg);
                using var updater = Process.Start(start);
                // Do not exit until the helper has loaded successfully and is waiting for us.
                string ready = Path.Combine(stage, "ready");
                for (int i = 0; i < 100 && !File.Exists(ready); i++)
                {
                    if (updater.HasExited) throw new InvalidOperationException("Update helper failed to start.");
                    await Task.Delay(100);
                }
                if (!File.Exists(ready))
                {
                    updater.Kill();
                    updater.WaitForExit();
                    throw new TimeoutException("Update helper did not become ready.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log("Update skipped: " + ex.Message);
                if (stage != null)
                {
                    try { Directory.Delete(stage, true); } catch { }
                }
                return false;
            }
        }

        internal static void RequireTrustedUri(Uri uri, Uri origin)
        {
            if (uri.Scheme != Uri.UriSchemeHttps || uri.Host != origin.Host || uri.Port != origin.Port || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidDataException("Updates must use HTTPS on the configured origin.");
        }

        internal static async Task CopyBoundedAsync(Stream input, Stream output, long limit, CancellationToken token)
        {
            var buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await input.ReadAsync(buffer.AsMemory(), token)) > 0)
            {
                total += count;
                if (total > limit) throw new InvalidDataException("Update exceeds size limit.");
                await output.WriteAsync(buffer.AsMemory(0, count), token);
            }
        }

        internal static void ExtractPackage(string zip, string destination, Version expectedVersion)
        {
            Directory.CreateDirectory(destination);
            string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            using var archive = ZipFile.OpenRead(zip);
            long total = 0;
            var paths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                string path = Path.GetFullPath(Path.Combine(root, name));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || name.Contains(':') ||
                    name.Split(Path.DirectorySeparatorChar).Any(part => part == ".." || part.EndsWith(".") || part.EndsWith(" ")) ||
                    !paths.Add(path))
                    throw new InvalidDataException("Unsafe update path.");
                total += entry.Length;
                if (total > MaxExtractedBytes || archive.Entries.Count > 10000)
                    throw new InvalidDataException("Update extraction exceeds size limit.");
                if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                entry.ExtractToFile(path);
            }
            string assembly = Path.Combine(root, "Scanner.WPF.dll");
            if (!File.Exists(Path.Combine(root, "Scanner.WPF.exe")) || !File.Exists(assembly) ||
                !File.Exists(Path.Combine(root, "Scanner.WPF.runtimeconfig.json")) ||
                AssemblyName.GetAssemblyName(assembly).Version != expectedVersion)
                throw new InvalidDataException("Package layout or assembly version does not match manifest.");
        }

        private static void Log(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scanner", "Updates");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "update.log"), $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
            }
            catch { }
        }

        private sealed class UpdateManifest
        {
            public string Version { get; set; }
            public string PackageUrl { get; set; }
            public string Sha256 { get; set; }
        }
    }
}
