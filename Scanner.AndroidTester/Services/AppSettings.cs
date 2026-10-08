using System.Xml.Linq;
namespace Scanner.AndroidTester.Services;

public sealed record AppSettings(Uri UploadBaseUrl, string UploadApiKey)
{
    public static AppSettings Load()
    {
        using var stream = typeof(AppSettings).Assembly.GetManifestResourceStream("Scanner.AndroidTester.appsettings.xml")
            ?? throw new InvalidOperationException("缺少 appsettings.xml 配置文件。");
        return Parse(stream);
    }
    public static AppSettings Parse(Stream stream)
    {
        var settings = XDocument.Load(stream).Element("configuration")?.Element("appSettings")
            ?? throw new InvalidOperationException("appsettings.xml 缺少 configuration/appSettings 节点。");
        string Read(string key) => settings.Elements("add").SingleOrDefault(x => (string?)x.Attribute("key") == key)?.Attribute("value")?.Value
            ?? throw new InvalidOperationException($"appsettings.xml 缺少 {key} 配置。");
        var address = Read("UploadBaseUrl").Trim();
        if (!Uri.TryCreate(address.EndsWith('/') ? address : address + "/", UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("UploadBaseUrl 必须为有效的 HTTP/HTTPS 服务器地址。");
        return new(uri, Read("UploadApiKey").Trim());
    }
}
