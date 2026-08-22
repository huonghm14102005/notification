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
        IValidator<AcceptMultiChannelNotificationRequest> multiValidator,
        AcceptNotificationHandler handler, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var apiKeyId)
            || !Guid.TryParse(principal.FindFirstValue("device_id"), out var sourceDeviceId)) return Results.Unauthorized();
        if (body.ValueKind != JsonValueKind.Object) return Validation();
        var isMulti = body.TryGetProperty("channels", out _) || body.TryGetProperty("content", out _);
        var isLegacy = body.TryGetProperty("recipients", out _) || body.TryGetProperty("subject", out _) || body.TryGetProperty("body", out _);
        if (isMulti && isLegacy) return Results.UnprocessableEntity(new { error = "Contracts cannot be mixed", code = "CONTRACT_AMBIGUOUS", statusCode = 422 });
        if (isMulti)
        {
            var allowedMulti = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "senderKey", "channels", "content" };
            if (body.EnumerateObject().Any(x => !allowedMulti.Contains(x.Name))) return Validation();
            AcceptMultiChannelNotificationRequest? multi;
            try { multi = body.Deserialize<AcceptMultiChannelNotificationRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { return Validation(); }
            if (multi is null) return Validation();
            if (multi.Channels is { Length: > 1 } || multi.Channels?.FirstOrDefault()?.Targets is { Length: > 1 })
                return Results.UnprocessableEntity(new { error = "Multiple targets are not enabled", code = "MULTIPLE_TARGETS_NOT_ENABLED", statusCode = 422 });
            if (multi.Channels?.Any(x => !string.Equals(x.Type?.Trim(), "email", StringComparison.OrdinalIgnoreCase)) == true)
                return Results.UnprocessableEntity(new { error = "Channel is not supported", code = "CHANNEL_NOT_SUPPORTED", statusCode = 422 });
            if (multi.Content is not null && !string.Equals(multi.Content.Mode?.Trim(), "plaintext", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(multi.Content.Mode?.Trim(), "template", StringComparison.OrdinalIgnoreCase))
                return Results.UnprocessableEntity(new { error = "Content mode is not supported", code = "CONTENT_MODE_NOT_SUPPORTED", statusCode = 422 });
            if (body.TryGetProperty("content", out var rawContent) && rawContent.ValueKind == JsonValueKind.Object)
            {
                var mode = rawContent.TryGetProperty("mode", out var rawMode) ? rawMode.GetString()?.Trim() : null;
                var allowedContent = string.Equals(mode, "template", StringComparison.OrdinalIgnoreCase)
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mode", "templateCode", "data" }
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mode", "subject", "body" };
                if (rawContent.EnumerateObject().Any(x => !allowedContent.Contains(x.Name)))
                    return Results.UnprocessableEntity(new { error = "Content contracts cannot be mixed", code = "CONTENT_CONTRACT_AMBIGUOUS", statusCode = 422 });
            }
            var checkedMulti = await multiValidator.ValidateAsync(multi, ct); if (!checkedMulti.IsValid) return Validation();
            var channel = multi.Channels![0]; var target = channel.Targets![0]; var content = multi.Content!;
            try
            {
                var input = string.Equals(content.Mode!.Trim(), "template", StringComparison.OrdinalIgnoreCase)
                    ? new NotificationContentInput("template", TemplateCode: content.TemplateCode, Data: content.Data)
                    : new NotificationContentInput("plaintext", content.Subject!.Trim(), content.Body);
                var accepted = await handler.HandleAsync(tenantId, apiKeyId, sourceDeviceId, new(multi.SenderKey, input,
                    new(target.Address.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(target.Ref) ? null : target.Ref.Trim())), ct);
                var item = accepted.Notifications[0];
                return Results.Json(new
                {
                    id = item.Id,
                    status = "accepted",
                    deliveries = new[] { new { id = item.DeliveryId,
                    channel = "email", target = item.Email, targetRef = item.Ref, status = "pending" } }
                }, statusCode: 202);
            }
            catch (NotificationOperationException exception) { return OperationError(exception); }
        }
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
            var accepted = await handler.HandleAsync(tenantId, apiKeyId, sourceDeviceId,
                new(request.SenderKey, new("plaintext", request.Subject.Trim(), request.Body),
                new(recipient.Email.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(recipient.Ref) ? null : recipient.Ref.Trim())), ct);
            return Results.Json(new { accepted = accepted.Accepted, notifications = accepted.Notifications.Select(x => new { x.Id, email = x.Email, @ref = x.Ref }) }, statusCode: StatusCodes.Status202Accepted);
        }
        catch (NotificationOperationException exception)
        {
            return OperationError(exception);
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
    private static IResult OperationError(NotificationOperationException exception) => exception.Code switch
    {
        "SENDER_NOT_FOUND" => Results.Conflict(new { error = "Sender not found", code = exception.Code, statusCode = 409 }),
        "TEMPLATE_NOT_FOUND" => Results.NotFound(new { error = "Template not found", code = exception.Code, statusCode = 404 }),
        "TEMPLATE_VARIABLE_MISSING" or "TEMPLATE_VARIABLE_UNKNOWN" or "TEMPLATE_RENDER_TOO_LARGE" =>
            Results.BadRequest(new { error = "Template rendering failed", code = exception.Code, statusCode = 400, names = exception.Names }),
        _ => Results.Json(new { error = "Service unavailable", code = exception.Code, statusCode = 503 }, statusCode: 503)
    };
    private static IResult NotFound() => Results.NotFound(new { error = "Not found", code = "NOT_FOUND", statusCode = 404 });
    private static string ToCamelPath(string path) => string.IsNullOrEmpty(path) ? path : char.ToLowerInvariant(path[0]) + path[1..];
}
