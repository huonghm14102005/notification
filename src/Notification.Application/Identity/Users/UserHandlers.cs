using System.Text;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Domain.Identity;

namespace Notification.Application.Identity.Users;

public sealed class UserHandlers(IUserRepository repository, IPasswordHasher passwordHasher, IClock clock)
{
    public async Task<UserItem> CreateAsync(Guid tenantId, CreateUserCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(command.DisplayName) ? email.Split('@')[0] : command.DisplayName.Trim();
        var user = new Admin(Guid.NewGuid(), tenantId, email, passwordHasher.Hash(command.Password), clock.UtcNow, AdminRole.Member, displayName);
        if (await repository.TryAddAsync(user, ct) == CreateUserResult.EmailExists) throw new UserOperationException("EMAIL_ALREADY_EXISTS");
        return await repository.GetAsync(tenantId, user.Id, ct) ?? throw new InvalidOperationException("Created user was not found.");
    }

    public async Task<UserItem> GetAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        await repository.GetAsync(tenantId, userId, ct) ?? throw new UserOperationException("USER_NOT_FOUND");

    public Task<UserPage> ListAsync(Guid tenantId, string? status, int limit, string? cursor, CancellationToken ct)
    { var (createdAt, id) = ParseCursor(cursor); return repository.ListAsync(tenantId, status, limit, createdAt, id, ct); }

    public async Task DisableAsync(Guid tenantId, Guid actorId, Guid userId, CancellationToken ct)
    {
        var result = await repository.DisableAsync(tenantId, actorId, userId, clock.UtcNow, ct);
        if (result == DisableUserResult.NotFound) throw new UserOperationException("USER_NOT_FOUND");
        if (result == DisableUserResult.CannotDisableSelf) throw new UserOperationException("CANNOT_DISABLE_SELF");
    }

    private static (DateTimeOffset?, Guid?) ParseCursor(string? cursor)
    {
        if (cursor is null) return (null, null);
        try { var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|'); return (DateTimeOffset.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind), Guid.Parse(parts[1])); }
        catch { throw new UserOperationException("VALIDATION_FAILED"); }
    }
}
