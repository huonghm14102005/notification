using System.Security.Claims;
using Notification.Application.Identity.Users;

namespace Notification.Api.Middleware;

public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserRepository users)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.FindFirstValue("actor_type") != "machine"
            && Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && !await users.IsActiveAsync(tenantId, userId, context.RequestAborted))
        { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", code = "UNAUTHORIZED", statusCode = 401 }); return; }
        await next(context);
    }
}
