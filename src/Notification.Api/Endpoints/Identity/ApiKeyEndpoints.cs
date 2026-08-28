using System.Security.Claims;
using FluentValidation;
using Notification.Api.Contracts.Identity;
using Notification.Application.Identity.ApiKeys;

namespace Notification.Api.Endpoints.Identity;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/api-keys", CreateAsync).RequireAuthorization("Admin").RequireRateLimiting("api-key-create");
        endpoints.MapGet("/v1/api-keys", ListAsync).RequireAuthorization("Admin");
        endpoints.MapDelete("/v1/api-keys/{id:guid}", RevokeAsync).RequireAuthorization("Admin");
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateApiKeyRequest request, IValidator<CreateApiKeyRequest> validator, ApiKeyHandlers handler, ClaimsPrincipal principal, HttpContext context, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Validation(validation.Errors);
        if (!TryIdentity(principal, out var tenantId, out var adminId)) return Results.Unauthorized();
        try
        {
            var created = await handler.CreateAsync(tenantId, adminId, request.ProducerName, ct); context.Response.Headers.CacheControl = "no-store"; AddDeprecation(context);
            return Results.Created($"/v1/api-keys/{created.Id}", new { id = created.Id, producerName = created.ProducerName, keyPrefix = created.KeyPrefix, key = created.RawKey, status = created.Status, createdAt = created.CreatedAt });
        }
        catch (ApiKeyOperationException exception) { return Error(exception.Code); }
    }

    private static async Task<IResult> ListAsync(int? limit, string? cursor, ApiKeyHandlers handler, ClaimsPrincipal principal, HttpContext context, CancellationToken ct)
    {
        if (!TryIdentity(principal, out var tenantId, out _)) return Results.Unauthorized();
        var take = limit ?? 50; if (take is < 1 or > 100) return Error("VALIDATION_FAILED");
        try { var page = await handler.ListAsync(tenantId, take, cursor, ct); AddDeprecation(context); return Results.Ok(new { items = page.Items, nextCursor = page.NextCursor }); }
        catch (ApiKeyOperationException exception) { return Error(exception.Code); }
    }

    private static async Task<IResult> RevokeAsync(Guid id, ApiKeyHandlers handler, ClaimsPrincipal principal, HttpContext context, CancellationToken ct)
    {
        if (!TryIdentity(principal, out var tenantId, out _)) return Results.Unauthorized();
        try { await handler.RevokeAsync(tenantId, id, ct); AddDeprecation(context); return Results.NoContent(); }
        catch (ApiKeyOperationException exception) { return Error(exception.Code); }
    }

    private static bool TryIdentity(ClaimsPrincipal principal, out Guid tenantId, out Guid adminId)
    {
        var hasTenant = Guid.TryParse(principal.FindFirstValue("tenant_id"), out tenantId);
        var hasAdmin = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);
        return hasTenant && hasAdmin;
    }
    private static IResult Validation(IEnumerable<FluentValidation.Results.ValidationFailure> errors) => Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400, details = errors.Select(x => new { path = x.PropertyName, message = x.ErrorMessage }) });
    private static IResult Error(string code) => code switch { "NOT_FOUND" => Results.NotFound(new { error = "Not found", code, statusCode = 404 }), "API_KEY_LIMIT_REACHED" => Results.Conflict(new { error = "API key limit reached", code, statusCode = 409 }), _ => Results.BadRequest(new { error = "Validation failed", code, statusCode = 400 }) };
    private static void AddDeprecation(HttpContext? context) { if (context is null) return; context.Response.Headers["Deprecation"] = "true"; context.Response.Headers.Link = "</v1/devices>; rel=\"successor-version\""; }
}
