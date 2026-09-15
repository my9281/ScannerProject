namespace Scanner.Server.Model;

public sealed record DatabaseHealthStatus(bool IsHealthy, string Status, string? ServerVersion = null, string? Error = null);
