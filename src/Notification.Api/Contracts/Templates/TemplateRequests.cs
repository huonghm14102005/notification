using FluentValidation;
namespace Notification.Api.Contracts.Templates;

public sealed class CreateTemplateRequest
{
    public string? TemplateCode { get; init; }
    public string? Key { get; init; }
    public string? Scope { get; init; }
    public Guid? SourceDeviceId { get; init; }
    public string? Audience { get; init; }
    public string? Subject { get; init; }
    public string? TextBody { get; init; }
    public string? HtmlBody { get; init; }
    public string? Body { get; init; }
    public string[]? Variables { get; init; }
}
public sealed class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    public CreateTemplateRequestValidator() { RuleFor(x => x).Must(x => { var k = x.TemplateCode ?? x.Key; return k is not null && System.Text.RegularExpressions.Regex.IsMatch(k.Trim().ToLowerInvariant(), "^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$"); }); RuleFor(x => x.Subject).NotNull(); RuleFor(x => x.Variables).NotNull(); RuleFor(x => x).Must(x => x.TextBody is not null || x.HtmlBody is not null || x.Body is not null); RuleFor(x => x).Must(x => x.Scope is null or "tenant" or "source"); RuleFor(x => x).Must(x => x.Audience is null or "user" or "system"); RuleFor(x => x).Must(x => (x.Scope ?? "tenant") != "tenant" || x.SourceDeviceId is null); RuleFor(x => x).Must(x => (x.Scope ?? "tenant") != "source" || x.SourceDeviceId is not null); }
}
