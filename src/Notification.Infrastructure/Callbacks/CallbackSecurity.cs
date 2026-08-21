using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Callbacks;
using Notification.Infrastructure.Configuration;

namespace Notification.Infrastructure.Callbacks;

public sealed class CallbackSecretGenerator : ICallbackSecretGenerator
{
    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class CallbackTargetValidator(IOptions<CallbackOptions> options) : ICallbackTargetValidator
{
    public async Task<string> ValidateAsync(string url, CancellationToken ct)
    {
        if (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Fragment.Length > 0 ||
            !string.IsNullOrEmpty(uri.UserInfo) || (uri.Scheme != Uri.UriSchemeHttps &&
            !(options.Value.AllowInsecureHttp && uri.Scheme == Uri.UriSchemeHttp)))
            throw new CallbackTargetException("CALLBACK_URL_INVALID");
        await ResolveAsync(uri.DnsSafeHost, options.Value.AllowPrivateNetwork, ct);
        return uri.AbsoluteUri;
    }

    public static async Task<IPAddress[]> ResolvePublicAsync(string host, CancellationToken ct)
        => await ResolveAsync(host, false, ct);

    public static async Task<IPAddress[]> ResolveAsync(string host, bool allowPrivateNetwork, CancellationToken ct)
    {
        IPAddress[] addresses;
        try { addresses = await Dns.GetHostAddressesAsync(host, ct); }
        catch (Exception exception) when (exception is not OperationCanceledException) { throw new CallbackTargetException("CALLBACK_DNS_FAILED"); }
        if (addresses.Length == 0 || !allowPrivateNetwork && addresses.Any(IsBlocked)) throw new CallbackTargetException("CALLBACK_TARGET_BLOCKED");
        return addresses;
    }

    public static bool IsBlocked(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None) || address.IsIPv6Multicast || address.IsIPv6LinkLocal)
            return true;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return (address.GetAddressBytes()[0] & 0xfe) == 0xfc;
        var bytes = address.GetAddressBytes();
        return bytes[0] is 0 or 10 or 127 || bytes[0] >= 224 ||
            bytes[0] == 169 && bytes[1] == 254 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
            bytes[0] == 100 && bytes[1] is >= 64 and <= 127 ||
            bytes[0] == 192 && (bytes[1] == 0 || bytes[1] == 168) ||
            bytes[0] == 198 && (bytes[1] is 18 or 19 || bytes[1] == 51 && bytes[2] == 100) ||
            bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113;
    }
}
