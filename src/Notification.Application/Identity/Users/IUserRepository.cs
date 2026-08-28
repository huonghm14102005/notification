using Notification.Domain.Identity;

namespace Notification.Application.Identity.Users;

public interface IUserRepository
{
    Task<CreateUserResult> TryAddAsync(Admin user, CancellationToken cancellationToken);
    Task<UserItem?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<UserPage> ListAsync(Guid tenantId, string? status, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken);
    Task<bool> IsActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<DisableUserResult> DisableAsync(Guid tenantId, Guid actorId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}

public enum DisableUserResult { Success, NotFound, CannotDisableSelf }
