using Notification.Application.Senders;
using Notification.Domain.Senders;

namespace Notification.Application.Tests.Senders;

public sealed class SenderResolverTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  DaO-TaO  ", "dao-tao")]
    public async Task ResolveAsync_NormalizesKey(string? input, string? expected)
    {
        var repository = new StubRepository { Result = Sender() };
        var result = await new SenderResolver(repository).ResolveAsync(Guid.NewGuid(), input, default);

        Assert.Equal(repository.Result, result);
        Assert.Equal(expected, repository.ReceivedKey);
    }

    [Fact]
    public async Task ResolveAsync_WhenUnavailable_ThrowsSenderNotFound()
    {
        var exception = await Assert.ThrowsAsync<SenderOperationException>(() =>
            new SenderResolver(new StubRepository()).ResolveAsync(Guid.NewGuid(), "missing", default));

        Assert.Equal("SENDER_NOT_FOUND", exception.Code);
    }

    private static ResolvedSender Sender() => new(Guid.NewGuid(), Guid.NewGuid(), "dao-tao", "email", "smtp.test", 587, false, "user", [1, 2, 3], "from@test", "Test");

    private sealed class StubRepository : ISenderRepository
    {
        public ResolvedSender? Result { get; init; }
        public string? ReceivedKey { get; private set; }
        public Task<ResolvedSender?> ResolveAsync(Guid tenantId, string? key, CancellationToken ct) { ReceivedKey = key; return Task.FromResult(Result); }
        public Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(Sender sender, CancellationToken ct) => throw new NotSupportedException();
        public Task<SenderPage> ListAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Sender?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SaveUpdateAsync(Guid tenantId, Sender sender, bool? isDefault, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    }
}
