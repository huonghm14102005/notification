using System.Text;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Identity.Abstractions;
using Notification.Domain.Identity;

namespace Notification.Application.Identity.ApiKeys;

public sealed class ApiKeyHandlers(IIdentityRepository repository, IApiKeySecretService secrets, IClock clock)
{
    public async Task<CreatedApiKey> CreateAsync(Guid tenantId, Guid adminId, string producerName, CancellationToken ct)
    {
        var secret = secrets.Generate(); var now = clock.UtcNow;
        var entity = new ApiKey(Guid.NewGuid(), tenantId, adminId, producerName.Trim(), secret.Prefix, secret.Hash, now);
        if (!await repository.TryAddApiKeyAsync(entity, 50, ct)) throw new ApiKeyOperationException("API_KEY_LIMIT_REACHED");
        return new(entity.Id, entity.ProducerName, entity.KeyPrefix, secret.Raw, entity.Status, entity.CreatedAt);
    }

    public Task<ApiKeyPage> ListAsync(Guid tenantId, int limit, string? cursor, CancellationToken ct)
    {
        DateTimeOffset? createdAt = null; Guid? id = null;
        if (cursor is not null)
        {
            try
            {
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
                createdAt = DateTimeOffset.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                id = Guid.Parse(parts[1]);
            }
            catch (Exception exception) when (exception is FormatException or IndexOutOfRangeException) { throw new ApiKeyOperationException("VALIDATION_FAILED"); }
        }
        return repository.ListApiKeysAsync(tenantId, limit, createdAt, id, ct);
    }

    public async Task RevokeAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        if (!await repository.RevokeApiKeyAsync(tenantId, id, clock.UtcNow, ct)) throw new ApiKeyOperationException("NOT_FOUND");
    }
}
