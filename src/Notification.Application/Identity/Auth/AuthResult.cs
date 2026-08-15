namespace Notification.Application.Identity.Auth;

public sealed record AuthResult(string AccessToken, int AccessTokenExpiresIn, string RefreshToken, int RefreshTokenExpiresIn, Guid AdminId, Guid TenantId, string Role);
