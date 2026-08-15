using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Identity.Abstractions;

namespace Notification.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory loggerFactory, UrlEncoder encoder, IIdentityRepository repository, IApiKeySecretService secrets, IClock clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return AuthenticateResult.NoResult();
        var raw = header[7..];
        if (raw.Length != 71 || !raw.StartsWith("notify_", StringComparison.Ordinal) || raw[7..].Any(c => !Uri.IsHexDigit(c) || char.IsUpper(c))) return AuthenticateResult.Fail("Invalid API key.");
        var identity = await repository.FindActiveApiKeyAsync(secrets.GetPrefix(raw), Context.RequestAborted);
        if (identity is null || !secrets.FixedTimeEquals(identity.Hash, secrets.Hash(raw))) return AuthenticateResult.Fail("Invalid API key.");
        try { await repository.TouchApiKeyAsync(identity.Id, clock.UtcNow, TimeSpan.FromMinutes(5), Context.RequestAborted); }
        catch (Exception exception) { Logger.LogWarning(exception, "Failed to update API key usage telemetry for {ApiKeyId}", identity.Id); }
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, identity.Id.ToString()), new Claim("tenant_id", identity.TenantId.ToString()), new Claim("producer_name", identity.ProducerName), new Claim("actor_type", "machine") };
        return AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized; Response.Headers.WWWAuthenticate = "Bearer";
        await Response.WriteAsJsonAsync(new { error = "Unauthorized", code = "UNAUTHORIZED", statusCode = 401 });
    }
}
