using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Notification.Api.Contracts.Identity;
using Notification.Application.Identity.RegisterTenant;

namespace Notification.Api.Endpoints.Identity;

public static class RegisterTenantEndpoint
{
    public static IEndpointRouteBuilder MapRegisterTenant(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/tenants/register", HandleAsync).RequireRateLimiting("registration");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(RegisterTenantRequest request, IValidator<RegisterTenantRequest> validator, RegisterTenantHandler handler, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400, details = validation.Errors.Select(x => new { path = x.PropertyName, message = x.ErrorMessage }) });
        try
        {
            var result = await handler.HandleAsync(new(request.TenantName, request.TenantSlug, request.AdminEmail, request.AdminPassword), ct);
            return Results.Created($"/v1/tenants/{result.TenantId}", new { tenant = new { id = result.TenantId, name = result.TenantName, slug = result.TenantSlug }, admin = new { id = result.AdminId, email = result.AdminEmail, role = result.Role } });
        }
        catch (RegistrationConflictException exception)
        {
            return Results.Conflict(new { error = "Registration conflict", code = exception.Code, statusCode = 409 });
        }
    }
}
