using System.Globalization;
using System.Reflection;
using System.Text;
using Scanner.AndroidTester.Services;

void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
var messages = (Dictionary<string, (string English, string Spanish)>)typeof(UiText)
    .GetField("Messages", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
foreach (var (key, translations) in messages)
{
    var expected = CompositeFormat.Parse(key).MinimumArgumentCount;
    foreach (var text in new[] { translations.English, translations.Spanish })
    {
        Check(!string.IsNullOrWhiteSpace(text), "Empty translation: " + key);
        Check(!text.Any(c => c is >= '\u4E00' and <= '\u9FFF'), "Untranslated text: " + key);
        Check(CompositeFormat.Parse(text).MinimumArgumentCount == expected, "Mismatched placeholders: " + key);
    }
}
foreach (var (saved, expected) in new[] { ("zh-CN", "zh"), ("en-US", "en"), ("es-ES", "es") })
{
    UiText.Language = saved;
    Check(UiText.Language == expected, "Saved culture normalization: " + saved);
}
foreach (var language in new[] { "zh", "en", "es" })
{
    UiText.Language = language;
    Check(UiText.T("上传") == (language switch { "zh" => "上传", "en" => "Upload", _ => "Subir" }), "Language switching");
    Check(UiText.T("托盘查询") == (language switch { "zh" => "托盘查询", "en" => "Find pallet", _ => "Buscar palé" }), "Pallet lookup language");
    var value = UiText.F("{0}\n托盘 {1} · {2:yyyy-MM-dd HH:mm:ss}", "SN-AbC", 10,
        new DateTimeOffset(2026, 10, 10, 13, 14, 15, TimeSpan.Zero));
    Check(value.Contains("SN-AbC") && value.Contains("10") && value.Contains("2026-10-10 13:14:15"), "Data and date preservation");
    Check(UiText.Error(new ArgumentException("本批 SN 重复。")) == UiText.T("本批 SN 重复。"), "Known error translation");
    Check(UiText.Error(new InvalidOperationException("未知服务端中文错误")) == UiText.T("请求失败，请重试。"), "Unknown server error fallback");
    Check(UiText.Error(new HttpRequestException()) == UiText.T("无法连接服务器，未确认记录已保留。"), "Network error translation");
}
UiText.Language = "invalid";
Check(UiText.Language == "zh", "Invalid saved language fallback");
Console.WriteLine($"PASS: {messages.Count} trilingual entries, placeholders, switching, error messages and barcode/date preservation.");
