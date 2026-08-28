using System.Security.Claims;
using FluentValidation;
using Notification.Api.Contracts.Identity;
using Notification.Application.Identity.Users;

namespace Notification.Api.Endpoints.Identity;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/users/me", MeAsync).RequireAuthorization("User");
        endpoints.MapPost("/v1/users", CreateAsync).RequireAuthorization("Admin");
        endpoints.MapGet("/v1/users", ListAsync).RequireAuthorization("Admin");
        endpoints.MapGet("/v1/users/{id:guid}", GetAsync).RequireAuthorization("Admin");
        endpoints.MapPost("/v1/users/{id:guid}/disable", DisableAsync).RequireAuthorization("Admin");
        return endpoints;
    }

    private static async Task<IResult> MeAsync(UserHandlers handlers, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var userId)) return Results.Unauthorized(); return Results.Ok(await handlers.GetAsync(tenantId, userId, ct)); }
    private static async Task<IResult> CreateAsync(CreateUserRequest request, IValidator<CreateUserRequest> validator, UserHandlers handlers, ClaimsPrincipal principal, CancellationToken ct)
    { var result = await validator.ValidateAsync(request, ct); if (!result.IsValid) return Validation(result.Errors); if (!Identity(principal, out var tenantId, out _)) return Results.Unauthorized(); try { var user = await handlers.CreateAsync(tenantId, new(request.Email, request.Password, request.DisplayName), ct); return Results.Created($"/v1/users/{user.Id}", user); } catch (UserOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> ListAsync(string? status, int? limit, string? cursor, UserHandlers handlers, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out _)) return Results.Unauthorized(); var take = limit ?? 50; if (status is not null and not "active" and not "disabled" || take is < 1 or > 100) return Error("VALIDATION_FAILED"); try { return Results.Ok(await handlers.ListAsync(tenantId, status, take, cursor, ct)); } catch (UserOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> GetAsync(Guid id, UserHandlers handlers, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out _)) return Results.Unauthorized(); try { return Results.Ok(await handlers.GetAsync(tenantId, id, ct)); } catch (UserOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> DisableAsync(Guid id, UserHandlers handlers, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { await handlers.DisableAsync(tenantId, actorId, id, ct); return Results.NoContent(); } catch (UserOperationException e) { return Error(e.Code); } }
    private static bool Identity(ClaimsPrincipal p, out Guid tenantId, out Guid userId)
    {
        var tenantValid = Guid.TryParse(p.FindFirstValue("tenant_id"), out tenantId);
        var userValid = Guid.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        return tenantValid && userValid;
    }
    private static IResult Validation(IEnumerable<FluentValidation.Results.ValidationFailure> errors) => Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400, details = errors.Select(x => new { path = x.PropertyName, message = x.ErrorMessage }) });
    private static IResult Error(string code) => code switch { "USER_NOT_FOUND" => Results.NotFound(new { error = "Not found", code, statusCode = 404 }), "EMAIL_ALREADY_EXISTS" or "CANNOT_DISABLE_SELF" => Results.Conflict(new { error = "Conflict", code, statusCode = 409 }), _ => Results.BadRequest(new { error = "Validation failed", code, statusCode = 400 }) };
}
