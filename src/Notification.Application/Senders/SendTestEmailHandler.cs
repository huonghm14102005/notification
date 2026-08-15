using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Time;

namespace Notification.Application.Senders;

public sealed class SendTestEmailHandler(ISenderRepository repository, IEmailSender emailSender, IClock clock)
{
    public async Task<SendTestEmailResult> HandleAsync(Guid tenantId, Guid senderId, string recipientEmail, CancellationToken ct)
    {
        var sender = await repository.FindResolvedByIdAsync(tenantId, senderId, ct)
            ?? throw new SenderOperationException("NOT_FOUND");
        if (sender.Status != "active") throw new SenderOperationException("SENDER_DISABLED");
        var now = clock.UtcNow;
        await emailSender.SendTestAsync(sender, recipientEmail, now, ct);
        if (!await repository.MarkVerifiedAsync(sender, now, ct)) throw new SenderOperationException("SENDER_CHANGED");
        return new(true, sender.Id, recipientEmail, now);
    }
}
