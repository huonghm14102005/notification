namespace Notification.Application.Abstractions.Security;

public interface IAccessTokenIssuer
{
    AccessTokenResult Issue(Guid adminId, Guid tenantId, string role, DateTimeOffset now);
}

public sealed record AccessTokenResult(string Token, int ExpiresIn);
