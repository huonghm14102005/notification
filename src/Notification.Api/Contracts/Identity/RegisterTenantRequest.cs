namespace Notification.Api.Contracts.Identity;

public sealed record RegisterTenantRequest(string TenantName, string TenantSlug, string AdminEmail, string AdminPassword);
