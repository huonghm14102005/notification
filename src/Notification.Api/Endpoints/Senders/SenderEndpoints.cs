using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Notification.Api.Contracts.Senders;
using Notification.Application.Abstractions.Email;
using Notification.Application.Senders;

namespace Notification.Api.Endpoints.Senders;

public static class SenderEndpoints
{
    public static IEndpointRouteBuilder MapSenderEndpoints(this IEndpointRouteBuilder e)
    {
        e.MapPost("/v1/senders", CreateAsync).RequireAuthorization("Admin").RequireRateLimiting("sender-mutation");
        e.MapGet("/v1/senders", ListAsync).RequireAuthorization("Admin");
        e.MapPatch("/v1/senders/{id:guid}", PatchAsync).RequireAuthorization("Admin").RequireRateLimiting("sender-mutation");
        e.MapDelete("/v1/senders/{id:guid}", DisableAsync).RequireAuthorization("Admin").RequireRateLimiting("sender-mutation");
        e.MapPost("/v1/senders/{id:guid}/test", TestAsync).RequireAuthorization("Admin").RequireRateLimiting("sender-test"); return e;
    }
    private static async Task<IResult> CreateAsync(CreateSenderRequest request, IValidator<CreateSenderRequest> validator, SenderHandlers h, ClaimsPrincipal p, CancellationToken ct)
    {
        var vr = await validator.ValidateAsync(request, ct); if (!vr.IsValid) return Validation(vr.Errors); if (!Tenant(p, out var tid)) return Results.Unauthorized();
        try { var item = await h.CreateAsync(tid, new(request.Key, request.Host, request.Port, request.Secure, request.Username, request.Password, request.FromEmail, request.FromName), ct); return Results.Created($"/v1/senders/{item.Id}", item); } catch (SenderOperationException x) { return Error(x.Code); }
    }
    private static async Task<IResult> ListAsync(int? limit, string? cursor, SenderHandlers h, ClaimsPrincipal p, CancellationToken ct)
    {
        if (!Tenant(p, out var tid)) return Results.Unauthorized(); var take = limit ?? 50; if (take is < 1 or > 100) return Error("VALIDATION_FAILED");
        try { var page = await h.ListAsync(tid, take, cursor, ct); return Results.Ok(new { items = page.Items, nextCursor = page.NextCursor }); } catch (SenderOperationException x) { return Error(x.Code); }
    }
    private static async Task<IResult> PatchAsync(Guid id, JsonElement body, IValidator<PatchSenderRequest> validator, SenderHandlers h, ClaimsPrincipal p, CancellationToken ct)
    {
        if (!Tenant(p, out var tid)) return Results.Unauthorized(); if (body.ValueKind != JsonValueKind.Object || !body.EnumerateObject().Any()) return Error("VALIDATION_FAILED");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "host", "port", "secure", "username", "password", "fromEmail", "fromName", "isDefault" };
        if (body.EnumerateObject().Any(x => !allowed.Contains(x.Name) || x.Value.ValueKind == JsonValueKind.Null)) return Error("VALIDATION_FAILED");
        PatchSenderRequest? request; try { request = body.Deserialize<PatchSenderRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch (JsonException) { return Error("VALIDATION_FAILED"); }
        if (request is null) return Error("VALIDATION_FAILED"); var vr = await validator.ValidateAsync(request, ct); if (!vr.IsValid) return Validation(vr.Errors);
        try { return Results.Ok(await h.UpdateAsync(tid, id, new(request.Host, request.Port, request.Secure, request.Username, request.Password, request.FromEmail, request.FromName, request.IsDefault), ct)); } catch (SenderOperationException x) { return Error(x.Code); }
    }
    private static async Task<IResult> DisableAsync(Guid id, SenderHandlers h, ClaimsPrincipal p, CancellationToken ct) { if (!Tenant(p, out var tid)) return Results.Unauthorized(); try { await h.DisableAsync(tid, id, ct); return Results.NoContent(); } catch (SenderOperationException x) { return Error(x.Code); } }
    private static async Task<IResult> TestAsync(Guid id, JsonElement body, IValidator<SendTestEmailRequest> validator, SendTestEmailHandler handler, ClaimsPrincipal p, CancellationToken ct)
    {
        if (!Tenant(p, out var tid)) return Results.Unauthorized();
        if (body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Count() != 1 || !body.EnumerateObject().Any(x => string.Equals(x.Name, "recipientEmail", StringComparison.OrdinalIgnoreCase) && x.Value.ValueKind == JsonValueKind.String)) return Error("VALIDATION_FAILED");
        SendTestEmailRequest? request; try { request = body.Deserialize<SendTestEmailRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch (JsonException) { return Error("VALIDATION_FAILED"); }
        if (request is null) return Error("VALIDATION_FAILED"); var vr = await validator.ValidateAsync(request, ct); if (!vr.IsValid) return Validation(vr.Errors);
        var recipient = request.RecipientEmail.Trim().ToLowerInvariant();
        try { return Results.Ok(await handler.HandleAsync(tid, id, recipient, ct)); }
        catch (SenderOperationException x) { return Error(x.Code); }
        catch (EmailSendException x) { return x.Code == "SMTP_TIMEOUT" ? Results.Json(new { error = "SMTP test timed out", code = "SMTP_TEST_TIMEOUT", statusCode = 504 }, statusCode: 504) : Results.Json(new { error = "SMTP test failed", code = "SMTP_TEST_FAILED", statusCode = 502, reason = x.Code }, statusCode: 502); }
    }
    private static bool Tenant(ClaimsPrincipal p, out Guid id) => Guid.TryParse(p.FindFirstValue("tenant_id"), out id);
    private static IResult Validation(IEnumerable<FluentValidation.Results.ValidationFailure> e) => Results.BadRequest(new { error = "Validation failed", code = "VALIDATION_FAILED", statusCode = 400, details = e.Select(x => new { path = x.PropertyName, message = x.ErrorMessage }) });
    private static IResult Error(string code) => code switch { "NOT_FOUND" => Results.NotFound(new { error = "Not found", code, statusCode = 404 }), "SENDER_KEY_EXISTS" or "SENDER_DISABLED" or "SENDER_CHANGED" => Results.Conflict(new { error = "Sender conflict", code, statusCode = 409 }), _ => Results.BadRequest(new { error = "Validation failed", code, statusCode = 400 }) };
}
