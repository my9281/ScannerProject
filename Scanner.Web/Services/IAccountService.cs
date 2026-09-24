using Scanner.Web.Models;

namespace Scanner.Web.Services;

public interface IAccountService
{
    IReadOnlyList<AccountDomain> GetDomains();
    AccountResult Register(RegisterRequest request);
    AccountResult? Login(LoginRequest request);
}

public sealed record AccountResult(string Username, AccountDomain Domain, string Token, DateTimeOffset ExpiresAt);

public sealed class AccountAlreadyExistsException(string username)
    : Exception($"用户名“{username}”已存在。");
