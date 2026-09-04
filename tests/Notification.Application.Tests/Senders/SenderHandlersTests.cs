using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Senders;
using Notification.Domain.Senders;

namespace Notification.Application.Tests.Senders;

public sealed class SenderHandlersTests
{
    [Fact]
    public async Task DeleteAsyncThrowsWhenNotFound()
    {
        var repo = new FakeSenderRepo { DeleteReturns = false };
        var handler = new SenderHandlers(repo, new FakeCipher(), new FakeClock(DateTimeOffset.UtcNow));
        var ex = await Assert.ThrowsAsync<SenderOperationException>(() =>
            handler.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task DeleteAsyncSucceedsWhenRepoReturnsTrue()
    {
        var repo = new FakeSenderRepo { DeleteReturns = true };
        var handler = new SenderHandlers(repo, new FakeCipher(), new FakeClock(DateTimeOffset.UtcNow));
        var tid = Guid.NewGuid();
        var id = Guid.NewGuid();
        await handler.DeleteAsync(tid, id, CancellationToken.None);
        Assert.Equal((tid, id), repo.LastDelete);
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
    private sealed class FakeCipher : ISecretCipher
    {
        public byte[] Encrypt(string plainText, Guid tenantId, Guid entityId) => [];
        public string Decrypt(byte[] cipherText, Guid tenantId, Guid entityId) => "";
    }
    private sealed class FakeSenderRepo : ISenderRepository
    {
        public bool DeleteReturns { get; set; }
        public (Guid, Guid) LastDelete { get; private set; }
        public Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            LastDelete = (tenantId, id);
            return Task.FromResult(DeleteReturns);
        }
        public Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Sender sender, CancellationToken ct) => Task.CompletedTask;
        public Task<SenderPage> ListAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct) => Task.FromResult(new SenderPage([], null));
        public Task<Sender?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<Sender?>(null);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SaveUpdateAsync(Guid tenantId, Sender sender, bool? isDefault, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
        public Task<ResolvedSender?> ResolveAsync(Guid tenantId, string? key, CancellationToken ct) => Task.FromResult<ResolvedSender?>(null);
        public Task<ResolvedSender?> FindResolvedByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<ResolvedSender?>(null);
        public Task<bool> MarkVerifiedAsync(ResolvedSender snapshot, DateTimeOffset now, CancellationToken ct) => Task.FromResult(true);
    }
}
