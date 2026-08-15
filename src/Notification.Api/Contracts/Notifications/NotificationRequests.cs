using FluentValidation;

namespace Notification.Api.Contracts.Notifications;

public sealed record NotificationRecipientRequest(string Email, string? Ref);
public sealed record AcceptNotificationRequest(string? SenderKey, string Subject, string Body, NotificationRecipientRequest[] Recipients);

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
