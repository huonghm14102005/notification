using Notification.Domain.Devices;
using Notification.Domain.Identity;

namespace Notification.Domain.Templates;

public static class TemplateScope { public const string Tenant = "tenant"; public const string Source = "source"; }
public static class TemplateAudience { public const string User = "user"; public const string System = "system"; }

public sealed class ContentTemplate
{
    private ContentTemplate() { }
    public ContentTemplate(Guid id, Guid tenantId, string templateCode, string scope, Guid? sourceDeviceId,
        string audience, int version, string subject, string? textBody, string? htmlBody, string[] variables, DateTimeOffset now)
    { Id = id; TenantId = tenantId; TemplateCode = templateCode; Scope = scope; SourceDeviceId = sourceDeviceId; Audience = audience; Version = version; Subject = subject; TextBody = textBody; HtmlBody = htmlBody; Variables = variables; Status = TemplateStatus.Draft; CreatedAt = UpdatedAt = now; }
    public ContentTemplate(Guid id, Guid tenantId, string key, string subject, string body, string[] variables, DateTimeOffset now)
        : this(id, tenantId, key, TemplateScope.Tenant, null, TemplateAudience.User, 1, subject, body, null, variables, now) { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TemplateCode { get; private set; } = string.Empty;
    public string Key => TemplateCode;
    public string Scope { get; private set; } = TemplateScope.Tenant;
    public Guid? SourceDeviceId { get; private set; }
    public string Audience { get; private set; } = TemplateAudience.User;
    public int Version { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string? TextBody { get; private set; }
    public string? HtmlBody { get; private set; }
    public string Body => TextBody ?? string.Empty;
    public string[] Variables { get; private set; } = [];
    public string Status { get; private set; } = TemplateStatus.Draft;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Device? SourceDevice { get; private set; }

    public void UpdateDraft(string? subject, string? textBody, bool setTextBody, string? htmlBody, bool setHtmlBody, string[]? variables, DateTimeOffset now)
    { if (Status != TemplateStatus.Draft) throw new InvalidOperationException("TEMPLATE_INVALID_STATE"); if (subject is not null) Subject = subject; if (setTextBody) TextBody = textBody; if (setHtmlBody) HtmlBody = htmlBody; if (variables is not null) Variables = variables; UpdatedAt = now; }
    public void Publish(DateTimeOffset now) { if (Status != TemplateStatus.Draft) throw new InvalidOperationException("TEMPLATE_INVALID_STATE"); Status = TemplateStatus.Active; PublishedAt = now; UpdatedAt = now; }
    public void Retire(DateTimeOffset now) { if (Status != TemplateStatus.Active) throw new InvalidOperationException("TEMPLATE_INVALID_STATE"); Status = TemplateStatus.Retired; RetiredAt = now; UpdatedAt = now; }
    public ContentTemplate CloneDraft(Guid id, int version, DateTimeOffset now) => Status == TemplateStatus.Active
        ? new(id, TenantId, TemplateCode, Scope, SourceDeviceId, Audience, version, Subject, TextBody, HtmlBody, Variables.ToArray(), now)
        : throw new InvalidOperationException("TEMPLATE_INVALID_STATE");
    public void Update(string? subject, string? body, string[]? variables, string? status, DateTimeOffset now)
    { if (Status == TemplateStatus.Draft) { UpdateDraft(subject, body, body is not null, null, false, variables, now); if (status == TemplateStatus.Active) Publish(now); else if (status is not null && status != TemplateStatus.Draft) throw new InvalidOperationException(); return; } if (Status == TemplateStatus.Active && status == TemplateStatus.Retired && subject is null && body is null && variables is null) { Retire(now); return; } throw new InvalidOperationException("TEMPLATE_INVALID_STATE"); }
}
