using System.Security.Cryptography;

namespace Scanner.Web.Services;

public static class AccountSecurity
{
    public static bool VerifyPassword(string password, byte[] salt, byte[] hash, string algorithm, uint iterations)
    {
        if (!string.Equals(algorithm, "PBKDF2-SHA256", StringComparison.OrdinalIgnoreCase)
            || iterations is 0 or > int.MaxValue || salt.Length is 0 or > 32 || hash.Length is 0 or > 64)
            return false;
        byte[] derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, (int)iterations, HashAlgorithmName.SHA256, hash.Length);
        try { return CryptographicOperations.FixedTimeEquals(derived, hash); }
        finally { CryptographicOperations.ZeroMemory(derived); }
    }

    public static bool CanLogin(string status, DateTime? lockedUntil, bool domainEnabled, string role, DateTime now) =>
        domainEnabled && IsKnownRole(role) && (lockedUntil is null || lockedUntil <= now)
        && (status == "active" || status == "locked" && lockedUntil.HasValue && lockedUntil <= now);

    public static bool IsKnownRole(string role) => role is "admin" or "user" or "viewer";
    public static string[] Permissions(string role) => role switch
    {
        "admin" => ["checklist.read", "checklist.write", "system.configure"],
        "user" => ["checklist.read", "checklist.write"],
        "viewer" => ["checklist.read"],
        _ => []
    };
    public static string LoginMessage(string role) => role switch
    {
        "admin" => "管理员登录成功，可查看、操作检测记录及配置系统。",
        "user" => "普通用户登录成功，可查看及操作检测记录。",
        "viewer" => "只读用户登录成功，仅可查看检测记录。",
        _ => "账户权限无效。"
    };
}
