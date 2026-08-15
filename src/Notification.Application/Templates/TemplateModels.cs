namespace Notification.Application.Templates;

public sealed record TemplateItem(Guid Id, string Key, string Subject, string Body, string[] Variables, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record TemplatePage(IReadOnlyList<TemplateItem> Items, string? NextCursor);
public sealed record CreateTemplateCommand(string Key, string Subject, string Body, string[] Variables);
public sealed record UpdateTemplateCommand(string? Subject, string? Body, string[]? Variables, string? Status);
public sealed record TemplateDefinition(Guid Id, string Key, string Subject, string Body, string[] Variables);
public sealed record RenderedContent(string Subject, string Body);
public sealed class TemplateOperationException(string code, IReadOnlyList<string>? names = null) : Exception(code) { public string Code { get; } = code; public IReadOnlyList<string>? Names { get; } = names; }
