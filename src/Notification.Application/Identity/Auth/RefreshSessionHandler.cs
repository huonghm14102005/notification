using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Identity.Abstractions;
using Notification.Domain.Identity;

namespace Notification.Application.Identity.Auth;

public sealed class RefreshSessionHandler(IIdentityRepository repository, IAccessTokenIssuer accessTokenIssuer, IRefreshTokenGenerator refreshTokenGenerator, IClock clock, AuthLifetime lifetime)
{
    public async Task<AuthResult> HandleAsync(string currentToken, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var secret = refreshTokenGenerator.Generate();
        var replacement = new RefreshToken(Guid.NewGuid(), Guid.Empty, Guid.Empty, secret.Hash, now, now.AddSeconds(lifetime.RefreshExpiresIn));
        var identity = await repository.RotateRefreshTokenAsync(refreshTokenGenerator.Hash(currentToken), replacement, now, cancellationToken)
            ?? throw new AuthenticationException("INVALID_REFRESH_TOKEN");
        var access = accessTokenIssuer.Issue(identity.AdminId, identity.TenantId, identity.Role, now);
        return new(access.Token, access.ExpiresIn, secret.Raw, lifetime.RefreshExpiresIn, identity.AdminId, identity.TenantId, identity.Role);
    }
}
