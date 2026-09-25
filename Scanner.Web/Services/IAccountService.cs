using Scanner.Web.Models;

namespace Scanner.Web.Services;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default);
    Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default);
    Task LogoutAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record AccountResult(string Username, string? DisplayName, AccountDomain Domain, string Role, string Token, DateTimeOffset ExpiresAt);

public sealed class AccountAlreadyExistsException(string username)
    : Exception($"用户名“{username}”已存在。");
