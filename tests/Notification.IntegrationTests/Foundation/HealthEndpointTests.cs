using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Notification.IntegrationTests.Foundation;

public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly TcpListener _postgres = new(IPAddress.Loopback, 0);
    private readonly TcpListener _redis = new(IPAddress.Loopback, 0);

    public Task InitializeAsync()
    {
        _postgres.Start();
        _redis.Start();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _postgres.Stop();
        _redis.Stop();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ReadinessAndLivenessFollowTheirContracts()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("no-store", ready.Headers.CacheControl?.ToString());

        _redis.Stop();
        ready = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var payload = await ready.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.Equal("unhealthy", payload?.Status);
        Assert.Equal("unhealthy", payload?.Checks?["redis"]);

        var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Theory]
    [InlineData("accepted.id-_1", "accepted.id-_1")]
    [InlineData("bad id\r\ninjected", null)]
    public async Task CorrelationIdIsPreservedOrReplaced(string requested, string? expected)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", requested);

        using var response = await client.SendAsync(request);
        var actual = response.Headers.GetValues("X-Correlation-ID").Single();
        if (expected is null) Assert.True(Guid.TryParse(actual, out _));
        else Assert.Equal(expected, actual);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = $"postgresql://notify:test@127.0.0.1:{((IPEndPoint)_postgres.LocalEndpoint).Port}/notification",
                ["REDIS_URL"] = $"redis://127.0.0.1:{((IPEndPoint)_redis.LocalEndpoint).Port}",
                ["HEALTH_CHECK_TIMEOUT_SECONDS"] = "1",
                ["SEED_TEST_ADMIN"] = "false",
                ["JWT_SECRET"] = "local-test-secret-at-least-32-bytes-long",
                ["API_KEY_SALT"] = "local-api-key-salt-at-least-16-bytes",
                ["ENCRYPTION_KEY"] = "MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MDE=",
            });
        }));

    private sealed record HealthPayload(string Status, Dictionary<string, string>? Checks);
}
