using System.Text;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Templates;
using Notification.Domain.Devices;
using Notification.Domain.Templates;
using Npgsql;
namespace Notification.Infrastructure.Persistence;

public sealed class TemplateRepository(NotificationDbContext db) : ITemplateRepository
{
    public Task<bool> FamilyExistsAsync(Guid t, string s, Guid? d, string k, CancellationToken c) => db.Templates.AnyAsync(x => x.TenantId == t && x.Scope == s && x.SourceDeviceId == d && x.TemplateCode == k, c);
    public Task<bool> SourceDeviceIsEligibleAsync(Guid t, Guid d, CancellationToken c) => db.Devices.AnyAsync(x => x.TenantId == t && x.Id == d && x.Status == DeviceStatus.Active && (x.Role == DeviceRole.Source || x.Role == DeviceRole.Both), c);
    public async Task AddAsync(ContentTemplate x, CancellationToken c) { db.Templates.Add(x); try { await db.SaveChangesAsync(c); } catch (DbUpdateException e) when (e.InnerException is PostgresException p && p.ConstraintName is "ux_templates_family_version" or "ux_templates_family_draft") { throw new TemplateOperationException(p.ConstraintName == "ux_templates_family_draft" ? "TEMPLATE_DRAFT_EXISTS" : "TEMPLATE_CODE_EXISTS"); } }
    public async Task<TemplatePage> ListAsync(Guid t, string? scope, Guid? source, string? audience, string? status, int limit, DateTimeOffset? at, Guid? id, CancellationToken c) { var q = db.Templates.AsNoTracking().Where(x => x.TenantId == t); if (scope is not null) q = q.Where(x => x.Scope == scope); if (source is not null) q = q.Where(x => x.SourceDeviceId == source); if (audience is not null) q = q.Where(x => x.Audience == audience); if (status is not null) q = q.Where(x => x.Status == status); if (at is not null && id is not null) q = q.Where(x => x.CreatedAt < at || (x.CreatedAt == at && x.Id.CompareTo(id.Value) < 0)); var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).ToListAsync(c); string? next = null; if (rows.Count > limit) { rows.RemoveAt(rows.Count - 1); var last = rows[^1]; next = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.CreatedAt:O}|{last.Id}")); } return new(rows.Select(TemplateHandlers.Map).ToList(), next); }
    public Task<ContentTemplate?> FindByIdAsync(Guid t, Guid id, CancellationToken c) => db.Templates.SingleOrDefaultAsync(x => x.TenantId == t && x.Id == id, c);
    public Task<ContentTemplate?> FindLegacyAsync(Guid t, string k, CancellationToken c) => db.Templates.Where(x => x.TenantId == t && x.Scope == TemplateScope.Tenant && x.TemplateCode == k).OrderByDescending(x => x.Version).FirstOrDefaultAsync(c);
    public async Task<int> GetNextVersionAsync(ContentTemplate x, CancellationToken c) => (await db.Templates.Where(y => y.TenantId == x.TenantId && y.Scope == x.Scope && y.SourceDeviceId == x.SourceDeviceId && y.TemplateCode == x.TemplateCode).MaxAsync(y => (int?)y.Version, c) ?? 0) + 1;
    public async Task PublishAsync(ContentTemplate draft, DateTimeOffset now, CancellationToken c) { await using var tx = await db.Database.BeginTransactionAsync(c); var active = await db.Templates.SingleOrDefaultAsync(x => x.TenantId == draft.TenantId && x.Scope == draft.Scope && x.SourceDeviceId == draft.SourceDeviceId && x.TemplateCode == draft.TemplateCode && x.Status == TemplateStatus.Active, c); if (active is not null) { active.Retire(now); await db.SaveChangesAsync(c); } draft.Publish(now); await db.SaveChangesAsync(c); await tx.CommitAsync(c); }
    public Task SaveAsync(CancellationToken c) => db.SaveChangesAsync(c);
    public Task<TemplateDefinition?> FindActiveAsync(Guid t, Guid source, string k, CancellationToken c) => db.Templates.AsNoTracking().Where(x => x.TenantId == t && x.TemplateCode == k && x.Status == TemplateStatus.Active && (x.SourceDeviceId == source || x.Scope == TemplateScope.Tenant)).OrderByDescending(x => x.SourceDeviceId == source).Select(x => new TemplateDefinition(x.Id, x.TemplateCode, x.Version, x.Subject, x.TextBody, x.HtmlBody, x.Variables)).FirstOrDefaultAsync(c);
}
