using System.Text.Json.Serialization;
namespace Notification.Application.Templates;

public sealed record TemplateItem(Guid Id, string TemplateCode, string Scope, Guid? SourceDeviceId, string Audience, int Version, string Subject, string? TextBody, string? HtmlBody, string[] Variables, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt, DateTimeOffset? RetiredAt)
{ [JsonIgnore] public string Key => TemplateCode; [JsonIgnore] public string Body => TextBody ?? string.Empty; }
public sealed record TemplatePage(IReadOnlyList<TemplateItem> Items, string? NextCursor);
public sealed record CreateTemplateCommand(string TemplateCode, string Scope, Guid? SourceDeviceId, string Audience, string Subject, string? TextBody, string? HtmlBody, string[] Variables);
public sealed record UpdateTemplateCommand(string? Subject, string? TextBody, bool SetTextBody, string? HtmlBody, bool SetHtmlBody, string[]? Variables);
public sealed record TemplateDefinition(Guid Id, string TemplateCode, int Version, string Subject, string? TextBody, string? HtmlBody, string[] Variables)
{ public TemplateDefinition(Guid id, string key, string subject, string body, string[] variables) : this(id, key, 1, subject, body, null, variables) { } }
public sealed record RenderedContent(string Subject, string? TextBody, string? HtmlBody) { [JsonIgnore] public string Body => TextBody ?? string.Empty; }
public sealed class TemplateOperationException(string code, IReadOnlyList<string>? names = null) : Exception(code) { public string Code { get; } = code; public IReadOnlyList<string>? Names { get; } = names; }
