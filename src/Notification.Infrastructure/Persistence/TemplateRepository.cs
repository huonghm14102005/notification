using System.Text;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Templates;
using Notification.Domain.Templates;
using Npgsql;
namespace Notification.Infrastructure.Persistence;

public sealed class TemplateRepository(NotificationDbContext db) : ITemplateRepository
{
    public Task<bool> KeyExistsAsync(Guid t, string k, CancellationToken c) => db.Templates.AnyAsync(x => x.TenantId == t && x.Key == k, c);
    public async Task AddAsync(ContentTemplate x, CancellationToken c) { db.Templates.Add(x); try { await db.SaveChangesAsync(c); } catch (DbUpdateException e) when (e.InnerException is PostgresException { ConstraintName: "ux_templates_tenant_key" }) { throw new TemplateOperationException("TEMPLATE_KEY_EXISTS"); } }
    public async Task<TemplatePage> ListAsync(Guid t, string? status, int limit, DateTimeOffset? at, Guid? id, CancellationToken c) { var q = db.Templates.AsNoTracking().Where(x => x.TenantId == t); if (status is not null) q = q.Where(x => x.Status == status); if (at is not null && id is not null) q = q.Where(x => x.CreatedAt < at || (x.CreatedAt == at && x.Id.CompareTo(id.Value) < 0)); var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).ToListAsync(c); string? next = null; if (rows.Count > limit) { rows.RemoveAt(rows.Count - 1); var last = rows[^1]; next = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.CreatedAt:O}|{last.Id}")); } return new(rows.Select(TemplateHandlers.Map).ToList(), next); }
    public Task<ContentTemplate?> FindAsync(Guid t, string k, CancellationToken c) => db.Templates.SingleOrDefaultAsync(x => x.TenantId == t && x.Key == k, c); public Task SaveAsync(CancellationToken c) => db.SaveChangesAsync(c);
    public Task<TemplateDefinition?> FindActiveAsync(Guid t, string k, CancellationToken c) => db.Templates.AsNoTracking().Where(x => x.TenantId == t && x.Key == k && x.Status == TemplateStatus.Active).Select(x => new TemplateDefinition(x.Id, x.Key, x.Subject, x.Body, x.Variables)).SingleOrDefaultAsync(c);
}
