using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Identity.Abstractions;

namespace Notification.Application.Identity.Auth;

public sealed class LogoutHandler(IIdentityRepository repository, IRefreshTokenGenerator refreshTokenGenerator, IClock clock)
{
    public async Task HandleAsync(string token, Guid adminId, CancellationToken cancellationToken)
    {
        var result = await repository.RevokeRefreshTokenAsync(refreshTokenGenerator.Hash(token), adminId, clock.UtcNow, cancellationToken);
        if (result is LogoutResult.Invalid) throw new AuthenticationException("INVALID_REFRESH_TOKEN");
    }
}
