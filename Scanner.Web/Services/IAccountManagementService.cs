using Scanner.Web.Models;

namespace Scanner.Web.Services;

public interface IAccountManagementService
{
    Task<IReadOnlyList<ManagedAccount>> List(AccountResult actor, ulong after, string search, CancellationToken ct);
    Task<ManagedAccount> Create(AccountResult actor, CreateAccountRequest request, CancellationToken ct);
    Task<IReadOnlyList<AccountDomain>> ListDomains(AccountResult actor, CancellationToken ct);
    Task<AccountDomain> CreateDomain(AccountResult actor, CreateDomainRequest request, CancellationToken ct);
    Task AssignDomain(AccountResult actor, ulong userId, AssignDomainRequest request, CancellationToken ct);
    Task ChangePassword(AccountResult actor, ChangeAccountPasswordRequest request, CancellationToken ct);
    Task ResetPassword(AccountResult actor, ulong userId, ResetAccountPasswordRequest request, CancellationToken ct);
}
