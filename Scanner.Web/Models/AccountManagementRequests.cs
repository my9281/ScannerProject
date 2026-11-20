using System.ComponentModel.DataAnnotations;

namespace Scanner.Web.Models;

public sealed class CreateAccountRequest
{
    [Required, StringLength(100)] public string Username { get; init; } = "";
    [StringLength(100)] public string? DisplayName { get; init; }
    [Range(1, int.MaxValue)] public int? DomainId { get; init; }
    [Required, RegularExpression("^(admin|user|viewer)$")] public string Role { get; init; } = "user";
    [Required, StringLength(4096, MinimumLength = 6)] public string Password { get; init; } = "";
    [Required, Compare(nameof(Password), ErrorMessage = "两次密码不一致。")]
    public string ConfirmPassword { get; init; } = "";
}

public sealed class ChangeAccountPasswordRequest
{
    [Required, StringLength(4096)] public string CurrentPassword { get; init; } = "";
    [Required, StringLength(4096, MinimumLength = 6)] public string NewPassword { get; init; } = "";
    [Required, Compare(nameof(NewPassword), ErrorMessage = "两次新密码不一致。")]
    public string ConfirmPassword { get; init; } = "";
}

public sealed class ResetAccountPasswordRequest
{
    [Required, StringLength(4096, MinimumLength = 6)] public string NewPassword { get; init; } = "";
    [Required, Compare(nameof(NewPassword), ErrorMessage = "两次新密码不一致。")]
    public string ConfirmPassword { get; init; } = "";
}

public sealed record ManagedAccount(string Id, string Username, string? DisplayName, string Role, string Status)
{
    public AccountDomain? Domain { get; init; }
}

public sealed record CreateDomainRequest([Required, StringLength(100)] string DomainName);
public sealed record AssignDomainRequest([Range(1, int.MaxValue)] int DomainId);
