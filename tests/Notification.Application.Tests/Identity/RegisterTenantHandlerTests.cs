using Notification.Application.Abstractions.Security;
using Notification.Application.Identity.Abstractions;
using Notification.Application.Identity.ApiKeys;
using Notification.Application.Identity.RegisterTenant;
using Notification.Domain.Identity;

namespace Notification.Application.Tests.Identity;

public sealed class RegisterTenantHandlerTests
{
    [Fact]
    public async Task NormalizesAndCreatesTenantAndOwnerTogether()
    {
        var repository = new RecordingRepository();
        var handler = new RegisterTenantHandler(repository, new FakeHasher());
        var result = await handler.HandleAsync(new(" Test Organization ", " TEST-ORG ", " ADMIN@LOCAL.TEST ", "12345678"), default);
        Assert.Equal("test-org", result.TenantSlug); Assert.Equal("admin@local.test", result.AdminEmail);
        Assert.Equal("hash:12345678", repository.Admin!.PasswordHash); Assert.Equal(repository.Tenant!.Id, repository.Admin.TenantId);
    }

    [Fact]
    public async Task RejectsExistingSlugBeforeWriting()
    {
        var repository = new RecordingRepository { SlugExists = true };
        var handler = new RegisterTenantHandler(repository, new FakeHasher());
        var error = await Assert.ThrowsAsync<RegistrationConflictException>(() => handler.HandleAsync(new("Test", "test-org", "admin@local.test", "12345678"), default));
        Assert.Equal("TENANT_SLUG_EXISTS", error.Code); Assert.Null(repository.Tenant);
    }

    private sealed class FakeHasher : IPasswordHasher { public string Hash(string password) => $"hash:{password}"; public bool Verify(string hash, string password) => hash == $"hash:{password}"; }
    private sealed class RecordingRepository : IIdentityRepository
    {
        public bool SlugExists { get; init; }
        public bool EmailExists { get; init; }
        public Tenant? Tenant { get; private set; }
        public Admin? Admin { get; private set; }
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) => Task.FromResult(SlugExists);
        public Task<bool> EmailExistsAsync(string email, CancellationToken ct) => Task.FromResult(EmailExists);
        public Task AddRegistrationAsync(Tenant tenant, Admin admin, CancellationToken ct) { Tenant = tenant; Admin = admin; return Task.CompletedTask; }
        public Task<Admin?> FindActiveAdminByEmailAsync(string email, CancellationToken ct) => Task.FromResult<Admin?>(null);
        public Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task<RefreshRotationResult?> RotateRefreshTokenAsync(byte[] currentHash, RefreshToken replacement, DateTimeOffset now, CancellationToken ct) => Task.FromResult<RefreshRotationResult?>(null);
        public Task<LogoutResult> RevokeRefreshTokenAsync(byte[] tokenHash, Guid adminId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(LogoutResult.Invalid);
        public Task<bool> TryAddApiKeyAsync(ApiKey apiKey, int activeLimit, CancellationToken ct) => Task.FromResult(false);
        public Task<ApiKeyPage> ListApiKeysAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct) => Task.FromResult(new ApiKeyPage([], null));
        public Task<bool> RevokeApiKeyAsync(Guid tenantId, Guid id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
        public Task<ApiKeyIdentity?> FindActiveApiKeyAsync(string prefix, CancellationToken ct) => Task.FromResult<ApiKeyIdentity?>(null);
        public Task TouchApiKeyAsync(Guid id, DateTimeOffset now, TimeSpan interval, CancellationToken ct) => Task.CompletedTask;
    }
}
