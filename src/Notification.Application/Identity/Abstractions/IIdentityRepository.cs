using Notification.Domain.Identity;

namespace Notification.Application.Identity.Abstractions;

public interface IIdentityRepository
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task AddRegistrationAsync(Tenant tenant, Admin admin, CancellationToken cancellationToken);
}
