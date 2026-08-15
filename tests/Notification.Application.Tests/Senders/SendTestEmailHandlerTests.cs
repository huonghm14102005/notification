using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Time;
using Notification.Application.Senders;
using Notification.Domain.Senders;

namespace Notification.Application.Tests.Senders;

public sealed class SendTestEmailHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-15T06:00:00Z");

    [Fact]
    public async Task SuccessfulSendMarksExactSnapshotVerified()
    {
        var repository = new StubRepository { Sender = Active(), Marked = true }; var email = new StubEmailSender();
        var result = await new SendTestEmailHandler(repository, email, new Clock()).HandleAsync(repository.Sender.TenantId, repository.Sender.Id, "admin@test", default);
        Assert.True(result.Sent); Assert.Equal(Now, result.VerifiedAt); Assert.Same(repository.Sender, repository.MarkedSnapshot); Assert.Equal(1, email.Calls);
    }

    [Fact]
    public async Task ChangedAfterAcceptanceDoesNotSendAgain()
    {
        var repository = new StubRepository { Sender = Active(), Marked = false }; var email = new StubEmailSender();
        var error = await Assert.ThrowsAsync<SenderOperationException>(() => new SendTestEmailHandler(repository, email, new Clock()).HandleAsync(repository.Sender.TenantId, repository.Sender.Id, "admin@test", default));
        Assert.Equal("SENDER_CHANGED", error.Code); Assert.Equal(1, email.Calls);
    }

    [Fact]
    public async Task DisabledSenderDoesNotOpenSmtp()
    {
        var repository = new StubRepository { Sender = Active() with { Status = SenderStatus.Disabled } }; var email = new StubEmailSender();
        var error = await Assert.ThrowsAsync<SenderOperationException>(() => new SendTestEmailHandler(repository, email, new Clock()).HandleAsync(repository.Sender.TenantId, repository.Sender.Id, "admin@test", default));
        Assert.Equal("SENDER_DISABLED", error.Code); Assert.Equal(0, email.Calls);
    }

    private static ResolvedSender Active() => new(Guid.NewGuid(), Guid.NewGuid(), "smtp", "email", "smtp.test", 587, false, "user", [1], "from@test", "Test");
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class StubEmailSender : IEmailSender { public int Calls { get; private set; } public Task SendTestAsync(ResolvedSender sender, string recipientEmail, DateTimeOffset now, CancellationToken ct) { Calls++; return Task.CompletedTask; } }
    private sealed class StubRepository : ISenderRepository
    {
        public required ResolvedSender Sender { get; init; }
        public bool Marked { get; init; }
        public ResolvedSender? MarkedSnapshot { get; private set; }
        public Task<ResolvedSender?> FindResolvedByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<ResolvedSender?>(Sender);
        public Task<bool> MarkVerifiedAsync(ResolvedSender snapshot, DateTimeOffset now, CancellationToken ct) { MarkedSnapshot = snapshot; return Task.FromResult(Marked); }
        public Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken ct) => throw new NotSupportedException(); public Task AddAsync(Sender sender, CancellationToken ct) => throw new NotSupportedException();
        public Task<SenderPage> ListAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct) => throw new NotSupportedException(); public Task<Sender?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException(); public Task SaveUpdateAsync(Guid tenantId, Sender sender, bool? isDefault, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException(); public Task<ResolvedSender?> ResolveAsync(Guid tenantId, string? key, CancellationToken ct) => throw new NotSupportedException();
    }
}
