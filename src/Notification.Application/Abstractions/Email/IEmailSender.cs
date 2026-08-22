using Notification.Application.Senders;

namespace Notification.Application.Abstractions.Email;

public interface IEmailSender
{
    Task SendTestAsync(ResolvedSender sender, string recipientEmail, DateTimeOffset now, CancellationToken ct);
    Task<string?> SendAsync(ResolvedSender sender, string recipientEmail, string subject, string body, CancellationToken ct);
    Task<string?> SendAsync(ResolvedSender sender, string recipientEmail, string subject, string? textBody,
        string? htmlBody, CancellationToken ct) => htmlBody is null && textBody is not null
            ? SendAsync(sender, recipientEmail, subject, textBody, ct)
            : throw new NotSupportedException("This email sender does not support HTML content.");
}

public sealed class EmailSendException(string code, bool isTransient) : Exception("Email sending failed.")
{
    public string Code { get; } = code;
    public bool IsTransient { get; } = isTransient;
}
