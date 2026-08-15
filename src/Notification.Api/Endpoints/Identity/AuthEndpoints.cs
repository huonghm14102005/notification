using System.Security.Claims;
using FluentValidation;
using Notification.Api.Contracts.Identity;
using Notification.Application.Identity.Auth;

namespace Notification.Api.Endpoints.Identity;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/auth/login", LoginAsync).RequireRateLimiting("login");
        endpoints.MapPost("/v1/auth/refresh", RefreshAsync);
        endpoints.MapPost("/v1/auth/logout", LogoutAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, IValidator<LoginRequest> validator, LoginHandler handler, HttpContext context, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationFailure(validation.Errors);
        try { return TokenResponse(await handler.HandleAsync(request.Email, request.Password, ct), context); }
        catch (AuthenticationException exception) { return Unauthorized(exception.Code); }
    }

    private static async Task<IResult> RefreshAsync(TokenRequest request, IValidator<TokenRequest> validator, RefreshSessionHandler handler, HttpContext context, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationFailure(validation.Errors);
        try { return TokenResponse(await handler.HandleAsync(request.RefreshToken, ct), context); }
        catch (AuthenticationException exception) { return Unauthorized(exception.Code); }
    }

    private static async Task<IResult> LogoutAsync(TokenRequest request, IValidator<TokenRequest> validator, LogoutHandler handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationFailure(validation.Errors);
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)) return Results.Unauthorized();
        try { await handler.HandleAsync(request.RefreshToken, adminId, ct); return Results.NoContent(); }
        catch (AuthenticationException exception) { return Unauthorized(exception.Code); }
    }

    private static IResult TokenResponse(AuthResult result, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        return Results.Ok(new { tokenType = "Bearer", accessToken = result.AccessToken, accessTokenExpiresIn = result.AccessTokenExpiresIn, refreshToken = result.RefreshToken, refreshTokenExpiresIn = result.RefreshTokenExpiresIn, admin = new { id = result.AdminId, tenantId = result.TenantId, role = result.Role } });
    }

    private static IResult Unauthorized(string code) => Results.Json(new { error = "Authentication failed", code, statusCode = 401 }, statusCode: 401);
    private static IResult ValidationFailure(IEnumerable<FluentValidation.Results.ValidationFailure> errors) => Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400, details = errors.Select(x => new { path = x.PropertyName, message = x.ErrorMessage }) });
}
