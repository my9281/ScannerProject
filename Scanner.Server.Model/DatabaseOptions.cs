namespace Scanner.Server.Model;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool Enabled { get; set; }
    public string Server { get; set; } = "localhost";
    public uint Port { get; set; } = 3306;
    public string Database { get; set; } = "scanner";
    public string UserId { get; set; } = "scanner";
    public string Password { get; set; } = string.Empty;
    public string SslMode { get; set; } = "Preferred";
    public uint ConnectionTimeout { get; set; } = 15;
    public uint CommandTimeout { get; set; } = 30;
}
