using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Bootstrap;

public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, string target, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        if (!context.Database.IsRelational()) return;
        if (target == "latest") await context.Database.MigrateAsync(cancellationToken);
        else await context.Database.MigrateAsync(target, cancellationToken);
    }
}
