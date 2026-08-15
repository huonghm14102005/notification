namespace Notification.Application.Identity.Auth;

public sealed class AuthenticationException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
