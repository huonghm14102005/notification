using Notification.Domain.Templates;
namespace Notification.Application.Templates;

public interface ITemplateRepository
{
    Task<bool> FamilyExistsAsync(Guid tenantId,string scope,Guid? sourceDeviceId,string code,CancellationToken ct);
    Task<bool> SourceDeviceIsEligibleAsync(Guid tenantId,Guid deviceId,CancellationToken ct);
    Task AddAsync(ContentTemplate template,CancellationToken ct);
    Task<TemplatePage> ListAsync(Guid tenantId,string? scope,Guid? sourceDeviceId,string? audience,string? status,int limit,DateTimeOffset? at,Guid? id,CancellationToken ct);
    Task<ContentTemplate?> FindByIdAsync(Guid tenantId,Guid id,CancellationToken ct);
    Task<ContentTemplate?> FindLegacyAsync(Guid tenantId,string code,CancellationToken ct);
    Task<int> GetNextVersionAsync(ContentTemplate template,CancellationToken ct);
    Task PublishAsync(ContentTemplate draft,DateTimeOffset now,CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<TemplateDefinition?> FindActiveAsync(Guid tenantId,Guid sourceDeviceId,string code,CancellationToken ct);

    Task<bool> KeyExistsAsync(Guid tenantId,string key,CancellationToken ct)=>FamilyExistsAsync(tenantId,"tenant",null,key,ct);
    Task<ContentTemplate?> FindAsync(Guid tenantId,string key,CancellationToken ct)=>FindLegacyAsync(tenantId,key,ct);
    Task<TemplateDefinition?> FindActiveAsync(Guid tenantId,string key,CancellationToken ct)=>FindActiveAsync(tenantId,Guid.Empty,key,ct);
}
