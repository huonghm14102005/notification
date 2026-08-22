using FluentValidation;

namespace Notification.Api.Contracts.Notifications;

public sealed record NotificationRecipientRequest(string Email, string? Ref);
public sealed record AcceptNotificationRequest(string? SenderKey, string Subject, string Body, NotificationRecipientRequest[] Recipients);
public sealed record NotificationTargetRequest(string Address, string? Ref);
public sealed record NotificationChannelRequest(string Type, NotificationTargetRequest[] Targets);
public sealed record NotificationContentRequest(string Mode, string? Subject, string? Body, string? TemplateCode,
    Dictionary<string, string>? Data);
public sealed record AcceptMultiChannelNotificationRequest(string? SenderKey, NotificationChannelRequest[] Channels,
    NotificationContentRequest Content);

public sealed class AcceptMultiChannelNotificationRequestValidator : AbstractValidator<AcceptMultiChannelNotificationRequest>
{
    public AcceptMultiChannelNotificationRequestValidator()
    {
        RuleFor(x => x.Channels).NotNull().Must(x => x is { Length: 1 });
        RuleForEach(x => x.Channels).ChildRules(channel =>
        {
            channel.RuleFor(x => x.Type).NotEmpty().Must(x => string.Equals(x?.Trim(), "email", StringComparison.OrdinalIgnoreCase));
            channel.RuleFor(x => x.Targets).NotNull().Must(x => x is { Length: 1 });
            channel.RuleForEach(x => x.Targets).ChildRules(target =>
            {
                target.RuleFor(x => x.Address).NotEmpty().MaximumLength(254).EmailAddress().Must(x => x is not null && !x.Contains(',') && !x.Contains(';'));
                target.RuleFor(x => x.Ref).Must(x => x is null || (x.Trim().Length <= 200 && !x.Any(char.IsControl)));
            });
        });
        RuleFor(x => x.Content).NotNull();
        When(x => x.Content is not null, () =>
        {
            RuleFor(x => x.Content.Mode).NotEmpty().Must(x => string.Equals(x?.Trim(), "plaintext", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x?.Trim(), "template", StringComparison.OrdinalIgnoreCase));
            When(x => string.Equals(x.Content.Mode?.Trim(), "plaintext", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Content.Subject).NotNull().Must(x => x is not null && x.Trim().Length is >= 1 and <= 998 && !x.Trim().Any(char.IsControl));
                RuleFor(x => x.Content.Body).NotNull().Must(x => x is not null && x.Length is >= 1 and <= 100000 && !x.Any(c => char.IsControl(c) && c is not '\t' and not '\r' and not '\n'));
            });
            When(x => string.Equals(x.Content.Mode?.Trim(), "template", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Content.TemplateCode).NotEmpty().Must(x => x is not null && System.Text.RegularExpressions.Regex.IsMatch(x.Trim().ToLowerInvariant(), "^[a-z0-9][a-z0-9._-]{2,62}$"));
                RuleFor(x => x.Content.Data).NotNull().Must(x => x is not null && x.Count <= 50
                    && x.All(p => p.Value is not null && p.Value.Length <= 10000));
            });
        });
    }
}

public sealed class AcceptNotificationRequestValidator : AbstractValidator<AcceptNotificationRequest>
{
    public AcceptNotificationRequestValidator()
    {
        RuleFor(x => x.Subject).NotNull().Must(x => x is not null && x.Trim().Length is >= 1 and <= 998 && !x.Trim().Any(char.IsControl));
        RuleFor(x => x.Body).NotNull().Must(x => x is not null && x.Length is >= 1 and <= 100000 && !x.Any(c => char.IsControl(c) && c is not '\t' and not '\r' and not '\n'));
        RuleFor(x => x.Recipients).NotNull().Must(x => x is { Length: 1 });
        RuleForEach(x => x.Recipients).ChildRules(recipient =>
        {
            recipient.RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress().Must(x => x is not null && !x.Contains(',') && !x.Contains(';'));
            recipient.RuleFor(x => x.Ref).Must(x => x is null || (x.Trim().Length <= 200 && !x.Any(char.IsControl)));
        });
    }
}
