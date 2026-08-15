using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Notification.Api.Contracts.Notifications;
using Notification.Application.Notifications;

namespace Notification.Api.Endpoints.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/notifications", Accept).RequireAuthorization("ApiKey"); return endpoints;
    }

    private static async Task<IResult> Accept(JsonElement body, IValidator<AcceptNotificationRequest> validator,
        AcceptNotificationHandler handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var apiKeyId)) return Results.Unauthorized();
        if (body.ValueKind != JsonValueKind.Object) return Validation();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "senderKey", "subject", "body", "recipients" };
        if (body.EnumerateObject().Any(x => !allowed.Contains(x.Name))) return Validation();
        var recipientFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email", "ref" };
        if (body.TryGetProperty("recipients", out var recipients) && recipients.ValueKind == JsonValueKind.Array
            && recipients.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.Object || x.EnumerateObject().Any(p => !recipientFields.Contains(p.Name)))) return Validation();
        AcceptNotificationRequest? request;
        try { request = body.Deserialize<AcceptNotificationRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch { return Validation(); }
        if (request is null) return Validation();
        var result = await validator.ValidateAsync(request, ct); if (!result.IsValid)
            return Results.BadRequest(new
            {
                error = "Validation failed",
                code = "VALIDATION_FAILED",
                statusCode = 400,
                errors = result.Errors.Select(x => new { field = ToCamelPath(x.PropertyName), message = x.ErrorMessage })
            });
        var recipient = request.Recipients[0];
        try
        {
            var accepted = await handler.HandleAsync(tenantId, apiKeyId, new(request.SenderKey, request.Subject.Trim(), request.Body,
                new(recipient.Email.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(recipient.Ref) ? null : recipient.Ref.Trim())), ct);
            return Results.Json(accepted, statusCode: StatusCodes.Status202Accepted);
        }
        catch (NotificationOperationException exception)
        {
            return exception.Code == "SENDER_NOT_FOUND"
                ? Results.Conflict(new { error = "Sender not found", code = exception.Code, statusCode = 409 })
                : Results.Json(new { error = "Service unavailable", code = exception.Code, statusCode = 503 }, statusCode: 503);
        }
    }

    private static IResult Validation() => Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400 });
    private static string ToCamelPath(string path) => string.IsNullOrEmpty(path) ? path : char.ToLowerInvariant(path[0]) + path[1..];
}
