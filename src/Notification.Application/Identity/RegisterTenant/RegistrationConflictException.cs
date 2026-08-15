namespace Notification.Application.Identity.RegisterTenant;

public sealed class RegistrationConflictException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
