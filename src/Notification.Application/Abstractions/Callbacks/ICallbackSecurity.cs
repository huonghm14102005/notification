namespace Notification.Application.Abstractions.Callbacks;

public interface ICallbackSecretGenerator
{
    string Generate();
}

public interface ICallbackTargetValidator
{
    Task<string> ValidateAsync(string url, CancellationToken cancellationToken);
}

public sealed class CallbackTargetException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
