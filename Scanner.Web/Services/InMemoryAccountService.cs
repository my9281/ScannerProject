using System.Collections.Concurrent;
using System.Security.Cryptography;
using Scanner.Web.Models;

namespace Scanner.Web.Services;

public sealed class InMemoryAccountService : IAccountService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);
    private static readonly IReadOnlyList<AccountDomain> Domains =
    [
        new(1, "Ymforever"),
        new(2, "Test1"),
        new(3, "Test2")
    ];
    private readonly ConcurrentDictionary<string, Account> _accounts =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AccountDomain> GetDomains() => Domains;

    public AccountResult Register(RegisterRequest request)
    {
        string username = request.Username.Trim();
        if (username.Length == 0)
            throw new ArgumentException("用户名不能为空。");
        AccountDomain domain = Domains.FirstOrDefault(item => item.Id == request.DomainId)
            ?? throw new ArgumentException("选择的域不存在。");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] passwordHash = HashPassword(request.Password, salt);
        var account = new Account(username, domain, salt, passwordHash);

        if (!_accounts.TryAdd(username, account))
            throw new AccountAlreadyExistsException(username);

        return CreateResult(account);
    }

    public AccountResult? Login(LoginRequest request)
    {
        string username = request.Username.Trim();
        if (!_accounts.TryGetValue(username, out Account? account))
            return null;

        byte[] suppliedHash = HashPassword(request.Password, account.PasswordSalt);
        return CryptographicOperations.FixedTimeEquals(suppliedHash, account.PasswordHash)
            ? CreateResult(account)
            : null;
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

    private static AccountResult CreateResult(Account account)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        return new AccountResult(
            account.Username,
            account.Domain,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            expiresAt);
    }

    private sealed record Account(
        string Username,
        AccountDomain Domain,
        byte[] PasswordSalt,
        byte[] PasswordHash);
}
