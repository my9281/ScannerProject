using System.IO;
using System.IO.Compression;
using Scanner.WPF.Services;

var temp = Path.Combine(Path.GetTempPath(), "ScannerUpdateTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var origin = new Uri("https://wms.ymforever.com/updates/wpf/latest.json");
    DesktopUpdateService.RequireTrustedUri(new Uri(origin, "test.zip"), origin);
    Reject(() => DesktopUpdateService.RequireTrustedUri(new Uri("http://wms.ymforever.com/test.zip"), origin));
    Reject(() => DesktopUpdateService.RequireTrustedUri(new Uri("https://other.example/test.zip"), origin));
    Reject(() => DesktopUpdateService.RequireTrustedUri(new Uri("https://user@wms.ymforever.com/test.zip"), origin));
    var valid = Path.Combine(temp, "valid.zip");
    using (var archive = ZipFile.Open(valid, ZipArchiveMode.Create))
    {
        archive.CreateEntryFromFile(typeof(DesktopUpdateService).Assembly.Location, "Scanner.WPF.dll");
        archive.CreateEntry("Scanner.WPF.exe");
        archive.CreateEntry("Scanner.WPF.runtimeconfig.json");
    }
    DesktopUpdateService.ExtractPackage(valid, Path.Combine(temp, "valid"), DesktopUpdateService.LocalVersion);
    Reject(() => DesktopUpdateService.ExtractPackage(valid, Path.Combine(temp, "mismatch"), new Version(99, 0, 0, 0)));
    foreach (string name in new[] { "../escape.dll", "C:/escape.dll", "data/file:stream", "data./file.dll" })
    {
        var zip = Path.Combine(temp, Guid.NewGuid() + ".zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) archive.CreateEntry(name);
        Reject(() => DesktopUpdateService.ExtractPackage(zip, Path.Combine(temp, Guid.NewGuid().ToString()), DesktopUpdateService.LocalVersion));
    }
    var duplicate = Path.Combine(temp, "duplicate.zip");
    using (var archive = ZipFile.Open(duplicate, ZipArchiveMode.Create))
    { archive.CreateEntry("one.dll"); archive.CreateEntry("ONE.dll"); }
    Reject(() => DesktopUpdateService.ExtractPackage(duplicate, Path.Combine(temp, "duplicates"), DesktopUpdateService.LocalVersion));
    try
    {
        await DesktopUpdateService.CopyBoundedAsync(new MemoryStream(new byte[11]), new MemoryStream(), 10, CancellationToken.None);
        throw new Exception("Oversized download accepted.");
    }
    catch (InvalidDataException) { }
    Console.WriteLine("PASS: HTTPS origin, valid package, version mismatch, unsafe ZIP paths, duplicates and download limit.");
}
finally { Directory.Delete(temp, true); }

static void Reject(Action action)
{
    try { action(); } catch (InvalidDataException) { return; }
    throw new Exception("Invalid update accepted.");
}
