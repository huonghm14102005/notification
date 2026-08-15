namespace Notification.Application.Abstractions.Security;

public interface IRefreshTokenGenerator
{
    RefreshTokenSecret Generate();
    byte[] Hash(string token);
}

public sealed record RefreshTokenSecret(string Raw, byte[] Hash);
