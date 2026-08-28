using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Notification.Infrastructure.Bootstrap;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        EnvFile.Load();
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "postgresql://notify:notify-local@localhost:5433/notification";

        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseNpgsql(DependencyInjection.ToConnectionString(databaseUrl));

        return new NotificationDbContext(optionsBuilder.Options);
    }
}
