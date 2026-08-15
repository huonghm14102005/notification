using Notification.Domain.Identity;

namespace Notification.Domain.Templates;

public sealed class ContentTemplate
{
    private ContentTemplate() { }
    public ContentTemplate(Guid id, Guid tenantId, string key, string subject, string body, string[] variables, DateTimeOffset now) { Id = id; TenantId = tenantId; Key = key; Subject = subject; Body = body; Variables = variables; Status = TemplateStatus.Draft; CreatedAt = UpdatedAt = now; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = "";
    public string Subject { get; private set; } = ""; public string Body { get; private set; } = ""; public string[] Variables { get; private set; } = [];
    public string Status { get; private set; } = TemplateStatus.Draft; public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public void Update(string? subject, string? body, string[]? variables, string? status, DateTimeOffset now)
    {
        if (Status == TemplateStatus.Retired) throw new InvalidOperationException("TEMPLATE_INVALID_STATE");
        if (status is not null && !((Status == TemplateStatus.Draft && status == TemplateStatus.Active) || (Status == TemplateStatus.Active && status == TemplateStatus.Retired) || status == Status)) throw new InvalidOperationException("TEMPLATE_INVALID_STATE");
        if (subject is not null) Subject = subject; if (body is not null) Body = body; if (variables is not null) Variables = variables; if (status is not null) Status = status; UpdatedAt = now;
    }
}
