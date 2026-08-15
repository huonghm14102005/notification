using System.Globalization;
using System.Text;
using Notification.Application.Abstractions.Time;
using Notification.Domain.Templates;
namespace Notification.Application.Templates;

public sealed class TemplateHandlers(ITemplateRepository repository, IClock clock)
{
    public async Task<TemplateItem> CreateAsync(Guid tid, CreateTemplateCommand c, CancellationToken ct) { var key = c.Key.Trim().ToLowerInvariant(); if (await repository.KeyExistsAsync(tid, key, ct)) throw new TemplateOperationException("TEMPLATE_KEY_EXISTS"); var vars = TemplateRenderer.Validate(c.Subject.Trim(), c.Body, c.Variables); var x = new ContentTemplate(Guid.NewGuid(), tid, key, c.Subject.Trim(), c.Body, vars, clock.UtcNow); await repository.AddAsync(x, ct); return Map(x); }
    public async Task<TemplateItem> GetAsync(Guid tid, string key, CancellationToken ct) => Map(await repository.FindAsync(tid, key.Trim().ToLowerInvariant(), ct) ?? throw new TemplateOperationException("NOT_FOUND"));
    public Task<TemplatePage> ListAsync(Guid tid, string? status, int limit, string? cursor, CancellationToken ct) { DateTimeOffset? at = null; Guid? id = null; if (cursor is not null) try { var p = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|'); at = DateTimeOffset.Parse(p[0], CultureInfo.InvariantCulture); id = Guid.Parse(p[1]); } catch { throw new TemplateOperationException("VALIDATION_FAILED"); } return repository.ListAsync(tid, status, limit, at, id, ct); }
    public async Task<TemplateItem> UpdateAsync(Guid tid, string key, UpdateTemplateCommand c, CancellationToken ct) { var x = await repository.FindAsync(tid, key.Trim().ToLowerInvariant(), ct) ?? throw new TemplateOperationException("NOT_FOUND"); if (x.Status == TemplateStatus.Retired) throw new TemplateOperationException("TEMPLATE_INVALID_STATE"); var subject = c.Subject?.Trim() ?? x.Subject; var body = c.Body ?? x.Body; var vars = TemplateRenderer.Validate(subject, body, c.Variables ?? x.Variables); try { x.Update(c.Subject?.Trim(), c.Body, c.Variables is null ? null : vars, c.Status, clock.UtcNow); } catch (InvalidOperationException) { throw new TemplateOperationException("TEMPLATE_INVALID_STATE"); } await repository.SaveAsync(ct); return Map(x); }
    public static TemplateItem Map(ContentTemplate x) => new(x.Id, x.Key, x.Subject, x.Body, x.Variables, x.Status, x.CreatedAt, x.UpdatedAt);
}
