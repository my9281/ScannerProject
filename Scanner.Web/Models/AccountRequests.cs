using System.ComponentModel.DataAnnotations;

namespace Scanner.Web.Models;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "用户名不能为空。")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "密码不能为空。")]
    public string Password { get; init; } = string.Empty;
}

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "用户名不能为空。")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "密码不能为空。")]
    [MinLength(6, ErrorMessage = "密码至少需要 6 个字符。")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "确认密码不能为空。")]
    [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致。")]
    public string ConfirmPassword { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "请选择有效的域。")]
    public int DomainId { get; init; }
}
