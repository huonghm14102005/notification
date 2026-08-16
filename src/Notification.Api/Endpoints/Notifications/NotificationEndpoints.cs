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
        endpoints.MapPost("/v1/notifications", Accept).RequireAuthorization("ApiKey");
        endpoints.MapGet("/v1/notifications/{id}", GetById).RequireAuthorization("AdminOrApiKey");
        return endpoints;
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

    private static async Task<IResult> GetById(
        string id,
        GetNotificationHandler handler,
        ClaimsPrincipal principal,
        CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId))
            return Results.Unauthorized();

        if (!Guid.TryParse(id, out var notificationId)) return NotFound();

        // Xác định loại caller: Admin hoặc ApiKey
        var callerType = principal.FindFirstValue("actor_type") == "machine" ? NotificationCallerType.ApiKey : NotificationCallerType.Admin;
        Guid? apiKeyId = null;
        if (callerType == NotificationCallerType.ApiKey)
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var keyId)) return Results.Unauthorized();
            apiKeyId = keyId;
        }

        var caller = new AuthCaller(callerType, apiKeyId);
        var query = new GetNotificationQuery(tenantId, notificationId, caller);

        try
        {
            var detail = await handler.HandleAsync(query, ct);
            if (detail is null) return NotFound();

            // Chuyển domain model thành API response
            var response = new GetNotificationResponse(
                detail.Id.ToString(),
                detail.TenantId.ToString(),
                detail.ProducerName,
                detail.SenderKey,
                detail.Status,
                detail.RecipientEmail,
                detail.RecipientRef,
                detail.Subject,
                detail.Body,
                detail.CreatedAt.ToString("o"),
                detail.SentAt?.ToString("o"),
                detail.UpdatedAt.ToString("o"),
                detail.FailureReason,
                detail.DeliveryAttempts
                    .Select(a => new DeliveryAttemptResponse(
                        a.AttemptNo,
                        a.Result,
                        a.StartedAt.ToString("o"),
                        a.FinishedAt.ToString("o"),
                        a.ErrorCode,
                        a.ErrorMessage,
                        a.ProviderMessageId))
                    .ToList());

            return Results.Ok(response);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Results.Json(new { error = "Internal server error", code = "INTERNAL_SERVER_ERROR", statusCode = 500 }, statusCode: 500);
        }
    }

    private static IResult Validation() => Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400 });
    private static IResult NotFound() => Results.NotFound(new { error = "Not found", code = "NOT_FOUND", statusCode = 404 });
    private static string ToCamelPath(string path) => string.IsNullOrEmpty(path) ? path : char.ToLowerInvariant(path[0]) + path[1..];
}
