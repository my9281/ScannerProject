using System.Globalization;
using System.Xml.Linq;

namespace Scanner.MaUI.Services;

public sealed class LocalizationService
{
    public static LocalizationService Current { get; } = new();
    private Dictionary<string, string> _values = new();
    public string Language { get; private set; } = "zh-CN";
    public event EventHandler? Changed;
    public string Get(string key) => _values.TryGetValue(key, out string? value) ? value : key;
    public string Format(string key, params object[] values) => string.Format(CultureInfo.GetCultureInfo(Language), Get(key), values);

    public void Change(string language)
    {
        if (language != "en-US" && language != "es-ES") language = "zh-CN";
        using Stream stream = typeof(LocalizationService).Assembly.GetManifestResourceStream("Language." + language + ".xaml")
            ?? throw new InvalidOperationException("Missing language resources: " + language);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var values = XDocument.Load(stream).Root!.Elements()
            .Where(element => element.Name.LocalName == "String")
            .ToDictionary(element => (string)element.Attribute(x + "Key")!, element => element.Value);
        Language = language;
        _values = values;
        foreach (var pair in values) Application.Current!.Resources[pair.Key] = pair.Value;
        Application.Current!.Resources["AppFontFamily"] = "GenJyuuGothic";
        Application.Current.Resources["PrintOptions"] = new[] { Get("PrintNone"), Get("PrintOnce"), Get("PrintTwice") };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
