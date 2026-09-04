using System.Security.Claims;
using FluentValidation;
using Notification.Api.Contracts.Devices;
using Notification.Application.Devices;

namespace Notification.Api.Endpoints.Devices;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/devices", CreateAsync).RequireAuthorization("User").RequireRateLimiting("device-create");
        endpoints.MapGet("/v1/devices", ListAsync).RequireAuthorization("User");
        endpoints.MapGet("/v1/devices/{id:guid}", GetAsync).RequireAuthorization("User");
        endpoints.MapPatch("/v1/devices/{id:guid}", RenameAsync).RequireAuthorization("User");
        endpoints.MapPost("/v1/devices/{id:guid}/disable", DisableAsync).RequireAuthorization("User");
        endpoints.MapDelete("/v1/devices/{id:guid}", DeleteAsync).RequireAuthorization("User");
        endpoints.MapPut("/v1/devices/{id:guid}/callback", ConfigureCallbackAsync).RequireAuthorization("User");
        endpoints.MapDelete("/v1/devices/{id:guid}/callback", ClearCallbackAsync).RequireAuthorization("User");
        endpoints.MapPost("/v1/devices/{deviceId:guid}/api-keys", CreateKeyAsync).RequireAuthorization("User").RequireRateLimiting("api-key-create");
        endpoints.MapGet("/v1/devices/{deviceId:guid}/api-keys", ListKeysAsync).RequireAuthorization("User");
        endpoints.MapDelete("/v1/devices/{deviceId:guid}/api-keys/{keyId:guid}", DeleteKeyAsync).RequireAuthorization("User");
        endpoints.MapPost("/v1/devices/{id:guid}/push-endpoint", RegisterPushEndpointAsync).RequireAuthorization("User");
        endpoints.MapGet("/v1/devices/{id:guid}/push-endpoint", GetPushEndpointAsync).RequireAuthorization("User");
        endpoints.MapDelete("/v1/devices/{id:guid}/push-endpoint", RevokePushEndpointAsync).RequireAuthorization("User");
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateDeviceRequest request, IValidator<CreateDeviceRequest> validator, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Validation(validation.Errors);
        if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized();
        var item = await handler.CreateAsync(tenantId, actorId, request.Name, request.Role, ct); return Results.Created($"/v1/devices/{item.Id}", item);
    }
    private static async Task<IResult> GetAsync(Guid id, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { return Results.Ok(await handler.GetAsync(tenantId, actorId, IsOwner(principal), id, ct)); } catch (DeviceOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> ListAsync(string? scope, string? status, int? limit, string? cursor, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); var take = limit ?? 50;
        if (scope is not null and not "mine" and not "tenant" || status is not null and not "active" and not "disabled" || take is < 1 or > 100) return Error("VALIDATION_FAILED");
        if (scope == "tenant" && !IsOwner(principal)) return Error("FORBIDDEN");
        try { return Results.Ok(await handler.ListAsync(tenantId, actorId, scope == "tenant", status, take, cursor, ct)); } catch (DeviceOperationException e) { return Error(e.Code); }
    }
    private static async Task<IResult> RenameAsync(Guid id, RenameDeviceRequest request, IValidator<RenameDeviceRequest> validator, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    { var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Validation(validation.Errors); if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { return Results.Ok(await handler.RenameAsync(tenantId, actorId, IsOwner(principal), id, request.Name, ct)); } catch (DeviceOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> DisableAsync(Guid id, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { await handler.DisableAsync(tenantId, actorId, IsOwner(principal), id, ct); return Results.NoContent(); } catch (DeviceOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> DeleteAsync(Guid id, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { await handler.DeleteAsync(tenantId, actorId, IsOwner(principal), id, ct); return Results.NoContent(); } catch (DeviceOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> ConfigureCallbackAsync(Guid id, ConfigureDeviceCallbackRequest request,
        IValidator<ConfigureDeviceCallbackRequest> validator, DeviceHandlers handler, ClaimsPrincipal principal,
        HttpContext context, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct); if (!validation.IsValid) return Validation(validation.Errors);
        if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized();
        try { var result = await handler.ConfigureCallbackAsync(tenantId, actorId, IsOwner(principal), id, request.Url, ct); context.Response.Headers.CacheControl = "no-store"; return Results.Ok(result); }
        catch (DeviceOperationException e) { return Error(e.Code); }
    }
    private static async Task<IResult> ClearCallbackAsync(Guid id, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { await handler.ClearCallbackAsync(tenantId, actorId, IsOwner(principal), id, ct); return Results.NoContent(); } catch (DeviceOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> CreateKeyAsync(Guid deviceId, DeviceHandlers handler, ClaimsPrincipal principal, HttpContext context, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { var key = await handler.CreateKeyAsync(tenantId, actorId, IsOwner(principal), deviceId, ct); context.Response.Headers.CacheControl = "no-store"; return Results.Created($"/v1/devices/{deviceId}/api-keys/{key.Id}", new { key.Id, key.DeviceId, key.KeyPrefix, key = key.RawKey, key.Status, key.CreatedAt }); } catch (DeviceOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> ListKeysAsync(Guid deviceId, int? limit, string? cursor, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); var take = limit ?? 50; if (take is < 1 or > 100) return Error("VALIDATION_FAILED"); try { return Results.Ok(await handler.ListKeysAsync(tenantId, actorId, IsOwner(principal), deviceId, take, cursor, ct)); } catch (DeviceOperationException e) { return Error(e.Code); } }
    private static async Task<IResult> DeleteKeyAsync(Guid deviceId, Guid keyId, DeviceHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    { if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized(); try { await handler.DeleteKeyAsync(tenantId, actorId, IsOwner(principal), deviceId, keyId, ct); return Results.NoContent(); } catch (DeviceOperationException e) { return Error(e.Code); } }

    private static async Task<IResult> RegisterPushEndpointAsync(Guid id, RegisterPushEndpointRequest request,
        IValidator<RegisterPushEndpointRequest> validator, PushEndpointHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Validation(validation.Errors);
        if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized();
        try
        {
            var result = await handler.RegisterAsync(tenantId, actorId, IsOwner(principal), id, request.Platform, request.Token, ct);
            return Results.Ok(result);
        }
        catch (DeviceOperationException e) { return Error(e.Code); }
    }

    private static async Task<IResult> GetPushEndpointAsync(Guid id, PushEndpointHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized();
        try
        {
            var endpoint = await handler.GetAsync(tenantId, actorId, IsOwner(principal), id, ct);
            return endpoint is null ? Results.NotFound(new { error = "Push endpoint not found", code = "NOT_FOUND", statusCode = 404 }) : Results.Ok(endpoint);
        }
        catch (DeviceOperationException e) { return Error(e.Code); }
    }

    private static async Task<IResult> RevokePushEndpointAsync(Guid id, PushEndpointHandlers handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Identity(principal, out var tenantId, out var actorId)) return Results.Unauthorized();
        try
        {
            var ok = await handler.RevokeAsync(tenantId, actorId, IsOwner(principal), id, ct);
            return ok ? Results.NoContent() : Results.NotFound(new { error = "Device not found", code = "NOT_FOUND", statusCode = 404 });
        }
        catch (DeviceOperationException e) { return Error(e.Code); }
    }

    private static bool Identity(ClaimsPrincipal p, out Guid tenantId, out Guid actorId)
    {
        var tenantValid = Guid.TryParse(p.FindFirstValue("tenant_id"), out tenantId);
        var actorValid = Guid.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
        return tenantValid && actorValid;
    }
    private static bool IsOwner(ClaimsPrincipal p) => p.IsInRole("owner");
    private static IResult Validation(IEnumerable<FluentValidation.Results.ValidationFailure> errors) => Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400, details = errors.Select(x => new { path = x.PropertyName, message = x.ErrorMessage }) });
    private static IResult Error(string code) => code switch
    {
        "NOT_FOUND" or "DEVICE_NOT_FOUND" => Results.NotFound(new { error = "Not found", code, statusCode = 404 }),
        "FORBIDDEN" => Results.Json(new { error = "Forbidden", code, statusCode = 403 }, statusCode: 403),
        "DEVICE_DISABLED" or "DEVICE_API_KEY_LIMIT_REACHED" or "API_KEY_LIMIT_REACHED" => Results.Conflict(new { error = "Conflict", code, statusCode = 409 }),
        _ => Results.BadRequest(new { error = "Validation failed", code, statusCode = 400 })
    };
}
