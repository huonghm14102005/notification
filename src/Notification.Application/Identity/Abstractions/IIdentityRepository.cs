using Notification.Application.Identity.ApiKeys;
using Notification.Domain.Identity;

namespace Notification.Application.Identity.Abstractions;

public interface IIdentityRepository
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task AddRegistrationAsync(Tenant tenant, Admin admin, CancellationToken cancellationToken);
    Task<Admin?> FindActiveAdminByEmailAsync(string email, CancellationToken cancellationToken);
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
    Task<RefreshRotationResult?> RotateRefreshTokenAsync(byte[] currentHash, RefreshToken replacement, DateTimeOffset now, CancellationToken cancellationToken);
    Task<LogoutResult> RevokeRefreshTokenAsync(byte[] tokenHash, Guid adminId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> TryAddApiKeyAsync(ApiKey apiKey, int activeLimit, CancellationToken cancellationToken);
    Task<ApiKeyPage> ListApiKeysAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken);
    Task<bool> RevokeApiKeyAsync(Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ApiKeyIdentity?> FindActiveApiKeyAsync(string prefix, CancellationToken cancellationToken);
    Task TouchApiKeyAsync(Guid id, DateTimeOffset now, TimeSpan interval, CancellationToken cancellationToken);
}

public sealed record RefreshRotationResult(Guid AdminId, Guid TenantId, string Role);
public enum LogoutResult { Success, Invalid }
