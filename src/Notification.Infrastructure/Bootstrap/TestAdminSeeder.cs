using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notification.Application.Identity.RegisterTenant;

namespace Notification.Infrastructure.Bootstrap;

public static class TestAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration, CancellationToken ct = default)
    {
        if (!bool.TryParse(configuration["SEED_TEST_ADMIN"], out var enabled) || !enabled) return;
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test")) throw new InvalidOperationException("SEED_TEST_ADMIN is only allowed in Development or Test.");
        using var scope = services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RegisterTenantHandler>();
        try { await handler.HandleAsync(new("Test Organization", "test-organization", "admin@local.test", "12345678"), ct); }
        catch (RegistrationConflictException exception) when (exception.Code is "TENANT_SLUG_EXISTS" or "ADMIN_EMAIL_EXISTS") { }
    }
}
