using Notification.Domain.Templates;
namespace Notification.Application.Templates;

public interface ITemplateRepository
{
    Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken ct); Task AddAsync(ContentTemplate template, CancellationToken ct);
    Task<TemplatePage> ListAsync(Guid tenantId, string? status, int limit, DateTimeOffset? at, Guid? id, CancellationToken ct); Task<ContentTemplate?> FindAsync(Guid tenantId, string key, CancellationToken ct); Task SaveAsync(CancellationToken ct);
    Task<TemplateDefinition?> FindActiveAsync(Guid tenantId, string key, CancellationToken ct);
}
