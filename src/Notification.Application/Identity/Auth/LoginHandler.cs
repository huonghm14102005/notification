using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Identity.Abstractions;
using Notification.Domain.Identity;

namespace Notification.Application.Identity.Auth;

public sealed class LoginHandler(IIdentityRepository repository, IPasswordHasher passwordHasher, IAccessTokenIssuer accessTokenIssuer, IRefreshTokenGenerator refreshTokenGenerator, IClock clock, AuthLifetime lifetime)
{
    public async Task<AuthResult> HandleAsync(string email, string password, CancellationToken cancellationToken)
    {
        var admin = await repository.FindActiveAdminByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (admin is null || !passwordHasher.Verify(admin.PasswordHash, password)) throw new AuthenticationException("INVALID_CREDENTIALS");
        var now = clock.UtcNow;
        var secret = refreshTokenGenerator.Generate();
        await repository.AddRefreshTokenAsync(new RefreshToken(Guid.NewGuid(), admin.Id, Guid.NewGuid(), secret.Hash, now, now.AddSeconds(lifetime.RefreshExpiresIn)), cancellationToken);
        return CreateResult(admin.Id, admin.TenantId, admin.Role, secret.Raw, now);
    }

    private AuthResult CreateResult(Guid adminId, Guid tenantId, string role, string refreshToken, DateTimeOffset now)
    {
        var access = accessTokenIssuer.Issue(adminId, tenantId, role, now);
        return new(access.Token, access.ExpiresIn, refreshToken, lifetime.RefreshExpiresIn, adminId, tenantId, role);
    }
}

public sealed record AuthLifetime(int RefreshExpiresIn);
