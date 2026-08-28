using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Callbacks;
using Notification.Application.Callbacks;
using Notification.Infrastructure.Configuration;

namespace Notification.Infrastructure.Callbacks;

public sealed class CallbackSender(IOptions<CallbackOptions> options) : ICallbackSender
{
    public async Task<CallbackSendResult> SendAsync(string url, string secret, string eventId, string rawJson,
        DateTimeOffset timestamp, CancellationToken ct)
    {
        var uri = new Uri(url); System.Net.IPAddress[] addresses;
        try { addresses = await CallbackTargetValidator.ResolveAsync(uri.DnsSafeHost, options.Value.AllowPrivateNetwork, ct); }
        catch (CallbackTargetException exception) { return new(false, exception.Code == "CALLBACK_DNS_FAILED", null, exception.Code); }
        var address = addresses[Random.Shared.Next(addresses.Length)];
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (context, token) =>
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(address, context.DnsEndPoint.Port, token); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); throw; }
            },
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(options.Value.TimeoutMs);
        var unix = timestamp.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = CallbackSignature.Create(secret, unix, rawJson);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.TryAddWithoutValidation("X-NTS-Event-Id", eventId);
        request.Headers.TryAddWithoutValidation("X-NTS-Timestamp", unix);
        request.Headers.TryAddWithoutValidation("X-NTS-Signature", signature);
        request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(rawJson));
        request.Content.Headers.ContentType = new("application/json");
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var status = (int)response.StatusCode;
            if (status is >= 200 and <= 299) return new(true, false, status, null);
            var transient = status is 408 or 425 or 429 || status is >= 500 and <= 599;
            return new(false, transient, status, status is >= 300 and <= 399 ? "CALLBACK_REDIRECT" : transient ? "CALLBACK_TRANSIENT_HTTP" : "CALLBACK_PERMANENT_HTTP");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return new(false, true, null, "CALLBACK_TIMEOUT"); }
        catch (AuthenticationException) { return new(false, false, null, "CALLBACK_TLS"); }
        catch (HttpRequestException exception) when (exception.InnerException is AuthenticationException) { return new(false, false, null, "CALLBACK_TLS"); }
        catch (Exception exception) when (exception is HttpRequestException or SocketException or IOException)
        { return new(false, true, null, "CALLBACK_CONNECTION"); }
    }
}
