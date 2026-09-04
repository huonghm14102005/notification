using Notification.Application.Abstractions.Callbacks;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Devices;
using Notification.Domain.Devices;
using Notification.Domain.Identity;

namespace Notification.Application.Tests.Devices;

public sealed class DeviceHandlersTests
{
    [Fact]
    public async Task DeleteAsyncThrowsWhenNotFound()
    {
        var repo = new FakeDeviceRepo { DeleteReturns = false };
        var handler = CreateHandler(repo);
        var ex = await Assert.ThrowsAsync<DeviceOperationException>(() =>
            handler.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), false, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task DeleteAsyncSucceedsWhenRepoReturnsTrue()
    {
        var repo = new FakeDeviceRepo { DeleteReturns = true };
        var handler = CreateHandler(repo);
        var tid = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var devId = Guid.NewGuid();
        await handler.DeleteAsync(tid, actor, true, devId, CancellationToken.None);
        Assert.Equal((tid, actor, true, devId), repo.LastDelete);
    }

    [Fact]
    public async Task DeleteKeyAsyncThrowsWhenNotFound()
    {
        var repo = new FakeDeviceRepo { DeleteKeyReturns = false };
        var handler = CreateHandler(repo);
        var ex = await Assert.ThrowsAsync<DeviceOperationException>(() =>
            handler.DeleteKeyAsync(Guid.NewGuid(), Guid.NewGuid(), false, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task DeleteKeyAsyncSucceedsWhenRepoReturnsTrue()
    {
        var repo = new FakeDeviceRepo { DeleteKeyReturns = true };
        var handler = CreateHandler(repo);
        var tid = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var devId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        await handler.DeleteKeyAsync(tid, actor, false, devId, keyId, CancellationToken.None);
        Assert.Equal((tid, actor, false, devId, keyId), repo.LastDeleteKey);
    }

    private static DeviceHandlers CreateHandler(IDeviceRepository repo) =>
        new(repo, new FakeSecrets(), new FakeCipher(), new FakeCallbackSecrets(), new FakeCallbackValidator(), new FakeClock(DateTimeOffset.UtcNow));

    private sealed class FakeClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
    private sealed class FakeCipher : ISecretCipher
    {
        public byte[] Encrypt(string plainText, Guid tenantId, Guid entityId) => [];
        public string Decrypt(byte[] cipherText, Guid tenantId, Guid entityId) => "";
    }
    private sealed class FakeSecrets : IApiKeySecretService
    {
        public ApiKeySecret Generate() => new("raw", "prefix", []);
        public string GetPrefix(string rawKey) => "prefix";
        public byte[] Hash(string rawKey) => [];
        public bool FixedTimeEquals(byte[] left, byte[] right) => true;
    }
    private sealed class FakeCallbackSecrets : ICallbackSecretGenerator
    {
        public string Generate() => "sec";
    }
    private sealed class FakeCallbackValidator : ICallbackTargetValidator
    {
        public Task<string> ValidateAsync(string url, CancellationToken ct) => Task.FromResult(url);
    }

    private sealed class FakeDeviceRepo : IDeviceRepository
    {
        public bool DeleteReturns { get; set; }
        public bool DeleteKeyReturns { get; set; }
        public (Guid, Guid, bool, Guid) LastDelete { get; private set; }
        public (Guid, Guid, bool, Guid, Guid) LastDeleteKey { get; private set; }

        public Task<bool> DeleteAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            LastDelete = (tenantId, actorId, tenantScope, deviceId);
            return Task.FromResult(DeleteReturns);
        }

        public Task<bool> DeleteKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, Guid keyId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            LastDeleteKey = (tenantId, actorId, tenantScope, deviceId, keyId);
            return Task.FromResult(DeleteKeyReturns);
        }

        public Task AddAsync(Device device, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DeviceItem> GetOrCreateLegacyAsync(Guid tenantId, Guid actorId, string producerName, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DeviceItem?> GetAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult<DeviceItem?>(null);
        public Task<DevicePage> ListAsync(Guid tenantId, Guid actorId, bool tenantScope, string? status, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken) => Task.FromResult(new DevicePage([], null));
        public Task<DeviceItem?> RenameAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string name, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<DeviceItem?>(null);
        public Task<bool> DisableAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ConfigureCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string url, byte[] secretEncrypted, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ClearCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<DeviceKeyCreateResult> TryAddKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, ApiKey apiKey, int deviceLimit, int tenantLimit, CancellationToken cancellationToken) => Task.FromResult(DeviceKeyCreateResult.Success);
        public Task<DeviceApiKeyPage?> ListKeysAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken) => Task.FromResult<DeviceApiKeyPage?>(null);
        public Task<bool> RevokeKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, Guid keyId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<DevicePushEndpoint?> FindPushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult<DevicePushEndpoint?>(null);
        public Task<DevicePushEndpoint?> FindActivePushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken) => Task.FromResult<DevicePushEndpoint?>(null);
        public Task SavePushEndpointAsync(DevicePushEndpoint endpoint, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> DisablePushEndpointAsync(Guid tenantId, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
