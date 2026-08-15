using FluentValidation;
namespace Notification.Api.Contracts.Templates;

public sealed record CreateTemplateRequest(string Key, string Subject, string Body, string[] Variables); public sealed record PatchTemplateRequest(string? Subject, string? Body, string[]? Variables, string? Status);
public sealed class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest> { public CreateTemplateRequestValidator() { RuleFor(x => x.Key).NotNull().Must(x => System.Text.RegularExpressions.Regex.IsMatch(x.Trim().ToLowerInvariant(), "^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$")); RuleFor(x => x.Subject).NotNull(); RuleFor(x => x.Body).NotNull(); RuleFor(x => x.Variables).NotNull(); } }
public sealed class PatchTemplateRequestValidator : AbstractValidator<PatchTemplateRequest> { public PatchTemplateRequestValidator() { RuleFor(x => x.Status).Must(x => x is null or "draft" or "active" or "retired"); } }
