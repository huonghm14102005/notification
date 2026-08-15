using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Bootstrap;

public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, string target, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database;
        if (target == "latest") await database.MigrateAsync(cancellationToken);
        else await database.MigrateAsync(target, cancellationToken);
    }
}
