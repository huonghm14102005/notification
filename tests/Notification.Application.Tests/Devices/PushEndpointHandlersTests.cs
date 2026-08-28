using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Devices;
using Notification.Domain.Devices;
using Notification.Domain.Identity;

namespace Notification.Application.Tests.Devices;

public sealed class PushEndpointHandlersTests
{
    [Fact]
    public async Task RegisterPushEndpointEncryptsTokenAndPersists()
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repo = new MockDeviceRepo(new DeviceItem(deviceId, "Mobile App", "recipient", "active", actorId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, false));
        var cipher = new MockCipher();
        var clock = new MockClock();
        var handlers = new PushEndpointHandlers(repo, cipher, clock);

        var result = await handlers.RegisterAsync(tenantId, actorId, true, deviceId, "fcm", "fcm_raw_token_123", CancellationToken.None);

        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal("fcm", result.Platform);
        Assert.Equal("active", result.Status);
        Assert.NotNull(repo.SavedEndpoint);
        Assert.Equal("enc:fcm_raw_token_123", System.Text.Encoding.UTF8.GetString(repo.SavedEndpoint.TokenEncrypted));
    }

    [Fact]
    public async Task RegisterToDisabledDeviceThrowsException()
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repo = new MockDeviceRepo(new DeviceItem(deviceId, "Mobile App", "recipient", "disabled", actorId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false));
        var handlers = new PushEndpointHandlers(repo, new MockCipher(), new MockClock());

        var ex = await Assert.ThrowsAsync<DeviceOperationException>(() =>
            handlers.RegisterAsync(tenantId, actorId, true, deviceId, "fcm", "token", CancellationToken.None));

        Assert.Equal("DEVICE_DISABLED", ex.Code);
    }

    [Fact]
    public async Task RevokeDisablesPushEndpoint()
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var repo = new MockDeviceRepo(new DeviceItem(deviceId, "Mobile App", "recipient", "active", actorId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, false));
        var handlers = new PushEndpointHandlers(repo, new MockCipher(), new MockClock());

        var ok = await handlers.RevokeAsync(tenantId, actorId, true, deviceId, CancellationToken.None);

        Assert.True(ok);
        Assert.True(repo.DisabledCalled);
    }

    private sealed class MockDeviceRepo(DeviceItem? device) : IDeviceRepository
    {
        public DevicePushEndpoint? SavedEndpoint { get; private set; }
        public bool DisabledCalled { get; private set; }

        public Task<DeviceItem?> GetAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(device);

        public Task<DevicePushEndpoint?> FindPushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(SavedEndpoint);

        public Task<DevicePushEndpoint?> FindActivePushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(SavedEndpoint?.Status == "active" ? SavedEndpoint : null);

        public Task SavePushEndpointAsync(DevicePushEndpoint endpoint, CancellationToken cancellationToken)
        {
            SavedEndpoint = endpoint;
            return Task.CompletedTask;
        }

        public Task<bool> DisablePushEndpointAsync(Guid tenantId, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            DisabledCalled = true;
            SavedEndpoint?.Disable(now);
            return Task.FromResult(true);
        }

        public Task AddAsync(Device device, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceItem> GetOrCreateLegacyAsync(Guid tenantId, Guid actorId, string producerName, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DevicePage> ListAsync(Guid tenantId, Guid actorId, bool tenantScope, string? status, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceItem?> RenameAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string name, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DisableAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ConfigureCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string url, byte[] secretEncrypted, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ClearCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceKeyCreateResult> TryAddKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, ApiKey apiKey, int deviceLimit, int tenantLimit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceApiKeyPage?> ListKeysAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RevokeKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, Guid keyId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MockCipher : ISecretCipher
    {
        public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetBytes("enc:" + plaintext);
        public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetString(envelope);
    }

    private sealed class MockClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
    }
}
