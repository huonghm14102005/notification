using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Notification.Application.Abstractions.Security;
using Notification.Infrastructure.Configuration;

namespace Notification.Infrastructure.Security;

public sealed class JwtAccessTokenIssuer(IOptions<AuthOptions> options) : IAccessTokenIssuer
{
    public AccessTokenResult Issue(Guid adminId, Guid tenantId, string role, DateTimeOffset now)
    {
        var value = options.Value;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new(JwtRegisteredClaimNames.Sub, adminId.ToString()),
                new("tenant_id", tenantId.ToString()),
                new(ClaimTypes.Role, role),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            Issuer = value.Issuer,
            Audience = value.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddSeconds(value.AccessExpiresIn).UtcDateTime,
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(value.Secret)), SecurityAlgorithms.HmacSha256),
        };
        return new(new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor), value.AccessExpiresIn);
    }
}
