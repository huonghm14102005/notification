namespace Notification.Application.Identity.RegisterTenant;

public sealed record RegisterTenantCommand(string TenantName, string TenantSlug, string AdminEmail, string AdminPassword);
public sealed record RegisteredTenant(Guid TenantId, string TenantName, string TenantSlug, Guid AdminId, string AdminEmail, string Role);
