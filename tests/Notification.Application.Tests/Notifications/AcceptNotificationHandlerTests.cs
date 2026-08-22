using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Notifications;
using Notification.Application.Senders;
using Notification.Application.Templates;
using Notification.Domain.Notifications;
using Notification.Domain.Templates;

namespace Notification.Application.Tests.Notifications;

public sealed class AcceptNotificationHandlerTests
{
    [Fact]
    public async Task AcceptedNotificationIsEncryptedAndPersisted()
    {
        var repository = new Repository(); using var metrics = new NotificationMetrics();
        var tenantId = Guid.NewGuid(); var apiKeyId = Guid.NewGuid(); var senderId = Guid.NewGuid();
        var handler = new AcceptNotificationHandler(repository, new Resolver(senderId, tenantId), new Templates(),
            new TemplateRenderer(), new Cipher(), new Clock(), metrics);

        var result = await handler.HandleAsync(tenantId, apiKeyId, Guid.NewGuid(),
            new(null, new("plaintext", "Subject", "Body"), new("student@example.test", "S1")), CancellationToken.None);

        Assert.Equal(1, result.Accepted); Assert.NotNull(repository.Value); Assert.Equal(NotificationStatus.Accepted, repository.Value.Status);
        Assert.Equal(apiKeyId, repository.Value.ApiKeyId); Assert.Equal(senderId, repository.Delivery!.SenderId);
        Assert.Equal("enc:Subject", System.Text.Encoding.UTF8.GetString(repository.Value.SubjectEncrypted));
        Assert.Equal("enc:Body", System.Text.Encoding.UTF8.GetString(repository.Value.TextBodyEncrypted!));
    }

    [Fact]
    public async Task MissingSenderDoesNotPersist()
    {
        var repository = new Repository(); using var metrics = new NotificationMetrics();
        var handler = new AcceptNotificationHandler(repository, new MissingResolver(), new Templates(),
            new TemplateRenderer(), new Cipher(), new Clock(), metrics);
        var error = await Assert.ThrowsAsync<NotificationOperationException>(() => handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new(null, new("plaintext", "Subject", "Body"), new("student@example.test", null)), CancellationToken.None));
        Assert.Equal("SENDER_NOT_FOUND", error.Code); Assert.Null(repository.Value);
    }

    [Fact]
    public async Task TemplateIsRenderedAndEncryptedBeforePersisting()
    {
        var repository = new Repository(); using var metrics = new NotificationMetrics();
        var tenantId = Guid.NewGuid(); var deviceId = Guid.NewGuid(); var templateId = Guid.NewGuid();
        var templates = new Templates(new(templateId, "score-result", 2, "Hi {{name}}", "Score {{score}}",
            "<b>{{score}}</b>", ["name", "score"]));
        var handler = new AcceptNotificationHandler(repository, new Resolver(Guid.NewGuid(), tenantId), templates,
            new TemplateRenderer(), new Cipher(), new Clock(), metrics);

        await handler.HandleAsync(tenantId, Guid.NewGuid(), deviceId,
            new(null, new("template", TemplateCode: " SCORE-RESULT ", Data: new Dictionary<string, string>
            { ["name"] = "An", ["score"] = "<9>" }), new("student@example.test", null)), CancellationToken.None);

        Assert.Equal(deviceId, templates.SourceDeviceId); Assert.Equal("score-result", templates.Code);
        Assert.Equal(templateId, repository.Value!.TemplateId);
        Assert.Equal("enc:Hi An", System.Text.Encoding.UTF8.GetString(repository.Value.SubjectEncrypted));
        Assert.Equal("enc:Score <9>", System.Text.Encoding.UTF8.GetString(repository.Value.TextBodyEncrypted!));
        Assert.Equal("enc:<b>&lt;9&gt;</b>", System.Text.Encoding.UTF8.GetString(repository.Value.HtmlBodyEncrypted!));
    }

    [Fact]
    public async Task MissingTemplateDoesNotPersist()
    {
        var repository = new Repository(); using var metrics = new NotificationMetrics();
        var handler = new AcceptNotificationHandler(repository, new Resolver(Guid.NewGuid(), Guid.NewGuid()), new Templates(),
            new TemplateRenderer(), new Cipher(), new Clock(), metrics);
        var error = await Assert.ThrowsAsync<NotificationOperationException>(() => handler.HandleAsync(Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), new(null, new("template", TemplateCode: "missing", Data: new Dictionary<string, string>()),
                new("student@example.test", null)), CancellationToken.None));
        Assert.Equal("TEMPLATE_NOT_FOUND", error.Code); Assert.Null(repository.Value);
    }

    private sealed class Repository : INotificationRepository
    {
        public OutboundNotification? Value { get; private set; }
        public Notification.Domain.Notifications.Delivery? Delivery { get; private set; }
        public Task AddAsync(OutboundNotification notification, Notification.Domain.Notifications.Delivery delivery, CancellationToken ct)
        {
            Value = notification; Delivery = delivery;
            return Task.CompletedTask;
        }
        public Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
    private sealed class Resolver(Guid senderId, Guid tenantId) : ISenderResolver { public Task<ResolvedSender> ResolveAsync(Guid tid, string? key, CancellationToken ct) => Task.FromResult(new ResolvedSender(senderId, tenantId, "default", "email", "smtp", 465, true, "user", [], "from@example.test", "From")); }
    private sealed class MissingResolver : ISenderResolver { public Task<ResolvedSender> ResolveAsync(Guid tenantId, string? senderKey, CancellationToken ct) => throw new SenderOperationException("SENDER_NOT_FOUND"); }
    private sealed class Cipher : ISecretCipher { public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetBytes("enc:" + plaintext); public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) => throw new NotSupportedException(); }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero); }
    private sealed class Templates(TemplateDefinition? active = null) : ITemplateRepository
    {
        public Guid SourceDeviceId { get; private set; }
        public string? Code { get; private set; }
        public Task<TemplateDefinition?> FindActiveAsync(Guid tenantId, Guid sourceDeviceId, string code, CancellationToken ct)
        { SourceDeviceId = sourceDeviceId; Code = code; return Task.FromResult(active); }
        public Task<bool> FamilyExistsAsync(Guid tenantId, string scope, Guid? sourceDeviceId, string code, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> SourceDeviceIsEligibleAsync(Guid tenantId, Guid deviceId, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(ContentTemplate template, CancellationToken ct) => throw new NotSupportedException();
        public Task<TemplatePage> ListAsync(Guid tenantId, string? scope, Guid? sourceDeviceId, string? audience, string? status, int limit, DateTimeOffset? at, Guid? id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ContentTemplate?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ContentTemplate?> FindLegacyAsync(Guid tenantId, string code, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> GetNextVersionAsync(ContentTemplate template, CancellationToken ct) => throw new NotSupportedException();
        public Task PublishAsync(ContentTemplate draft, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
