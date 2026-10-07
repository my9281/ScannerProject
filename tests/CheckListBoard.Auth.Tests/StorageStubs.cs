// HTTP/client contract tests only. Platform encryption must be verified on a real device.
namespace Microsoft.Maui.Storage;
public static class Preferences { public static TestPreferences Default { get; } = new(); }
public sealed class TestPreferences
{
    private readonly Dictionary<string, string> values = [];
    public string Get(string key, string fallback) => values.GetValueOrDefault(key, fallback);
    public void Set(string key, string value) => values[key] = value;
}
public static class SecureStorage { public static TestSecureStorage Default { get; } = new(); }
public sealed class TestSecureStorage
{
    private readonly Dictionary<string, string> values = [];
    public Task<string?> GetAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
    public Task SetAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
    public bool Remove(string key) => values.Remove(key);
}
