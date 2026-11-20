using System.Xml.Linq;
namespace Scanner.ShippingCowAndroid.Services;

public sealed record AppSettings(Uri UploadBaseUrl, string UploadApiKey)
{
    public string ReceivingUsername { get; init; } = "PDA01";
    public string ReceivingPassword { get; init; } = "";
    public override string ToString() => $"AppSettings {{ UploadBaseUrl = {UploadBaseUrl}, ReceivingUsername = {ReceivingUsername} }}";
    public static AppSettings Load()
    {
        using var stream = typeof(AppSettings).Assembly.GetManifestResourceStream("Scanner.ShippingCowAndroid.appsettings.xml")
            ?? throw new InvalidOperationException(UiText.T("缺少 appsettings.xml 配置文件。"));
        return Parse(stream);
    }
    public static AppSettings Parse(Stream stream)
    {
        var settings = XDocument.Load(stream).Element("configuration")?.Element("appSettings")
            ?? throw new InvalidOperationException(UiText.T("appsettings.xml 缺少 configuration/appSettings 节点。"));
        string Read(string key) => settings.Elements("add").SingleOrDefault(x => (string?)x.Attribute("key") == key)?.Attribute("value")?.Value
            ?? throw new InvalidOperationException(UiText.F("appsettings.xml 缺少 {0} 配置。", key));
        var address = Read("UploadBaseUrl").Trim();
        if (!Uri.TryCreate(address.EndsWith('/') ? address : address + "/", UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException(UiText.T("UploadBaseUrl 必须为有效的 HTTP/HTTPS 服务器地址。"));
        string Optional(string key, string fallback) => settings.Elements("add").SingleOrDefault(x => (string?)x.Attribute("key") == key)?.Attribute("value")?.Value ?? fallback;
        return new(uri, Read("UploadApiKey").Trim())
        {
            ReceivingUsername = Optional("ReceivingUsername", "PDA01").Trim(),
            // Password whitespace is significant, just as on the Web login endpoint.
            ReceivingPassword = Optional("ReceivingPassword", "")
        };
    }
}
