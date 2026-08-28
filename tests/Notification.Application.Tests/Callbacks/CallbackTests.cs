using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Callbacks;

namespace Notification.Application.Tests.Callbacks;

public sealed class CallbackTests
{
    [Fact]
    public void SignatureUsesTimestampDotRawBody()
    {
        Assert.Equal("v1=1d6e477e61e35990e7fa857d7f926950238de24665d51dacc1621e5a51c388fc",
            CallbackSignature.Create("secret", "1787202600", "{\"ok\":true}"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 25)]
    [InlineData(4, 120)]
    [InlineData(5, 720)]
    public async Task TransientFailureSchedulesBackoff(int attemptNo, int minutes)
    {
        var repository = new Repository(Item(attemptNo)); var clock = new Clock();
        var handler = new DeliverCallbackHandler(repository, new Sender(new(false, true, 503, "CALLBACK_TRANSIENT_HTTP")), new Cipher(), clock);
        Assert.Equal("retrying", await handler.HandleAsync(repository.Item.EventId, attemptNo, CancellationToken.None));
        Assert.Equal(clock.LastRead.AddMinutes(minutes), repository.NextAttemptAt);
    }

    [Fact]
    public async Task SixthTransientFailureIsTerminal()
    {
        var repository = new Repository(Item(6)); var handler = new DeliverCallbackHandler(repository,
            new Sender(new(false, true, null, "CALLBACK_TIMEOUT")), new Cipher(), new Clock());
        Assert.Equal("failed", await handler.HandleAsync(repository.Item.EventId, 6, CancellationToken.None));
        Assert.Null(repository.NextAttemptAt);
    }

    private static CallbackWorkItem Item(int attempt) => new(Guid.NewGuid(), $"evt_{Guid.NewGuid():N}", Guid.NewGuid(),
        Guid.NewGuid(), attempt, "https://example.test/callback", "secret"u8.ToArray(), "{}"u8.ToArray(), "sending");
    private sealed class Repository(CallbackWorkItem item) : ICallbackRepository
    {
        public CallbackWorkItem Item { get; } = item; public DateTimeOffset? NextAttemptAt { get; private set; }
        public Task<IReadOnlyList<ClaimedCallback>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<CallbackWorkItem?> LoadClaimedAsync(Guid eventId, int attemptNo, CancellationToken ct) => Task.FromResult<CallbackWorkItem?>(Item);
        public Task<bool> CancelClaimedAsync(Guid eventId, int attemptNo, DateTimeOffset cancelledAt, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> CompleteAsync(CallbackWorkItem value, CallbackSendResult result, DateTimeOffset startedAt, DateTimeOffset finishedAt, DateTimeOffset? nextAttemptAt, CancellationToken ct) { NextAttemptAt = nextAttemptAt; return Task.FromResult(true); }
        public Task<IReadOnlyList<ClaimedCallback>> RecoverStuckAsync(DateTimeOffset now, DateTimeOffset staleBefore, int limit, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Sender(CallbackSendResult result) : ICallbackSender
    { public Task<CallbackSendResult> SendAsync(string url, string secret, string eventId, string rawJson, DateTimeOffset timestamp, CancellationToken ct) => Task.FromResult(result); }
    private sealed class Cipher : ISecretCipher
    { public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => throw new NotSupportedException(); public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetString(envelope); }
    private sealed class Clock : IClock
    { private DateTimeOffset _now = new(2026, 8, 21, 0, 0, 0, TimeSpan.Zero); public DateTimeOffset LastRead { get; private set; } public DateTimeOffset UtcNow => LastRead = _now = _now.AddMilliseconds(1); }
}
