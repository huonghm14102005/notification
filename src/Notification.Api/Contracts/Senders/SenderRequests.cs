using FluentValidation;

namespace Notification.Api.Contracts.Senders;

public sealed record CreateSenderRequest(string Key, string Host, int Port, bool Secure, string Username, string Password, string FromEmail, string FromName);
public sealed record PatchSenderRequest(string? Host, int? Port, bool? Secure, string? Username, string? Password, string? FromEmail, string? FromName);

public static class SenderRules
{
    public static void Apply<T>(AbstractValidator<T> v, Func<T, string?> host, Func<T, int?> port, Func<T, string?> username, Func<T, string?> password, Func<T, string?> email, Func<T, string?> name)
    {
        v.RuleFor(x => host(x)).Must(x => x is null || IsHost(x.Trim())).WithMessage("Invalid SMTP host.");
        v.RuleFor(x => port(x)).Must(x => x is null || x is >= 1 and <= 65535);
        v.RuleFor(x => username(x)).Must(x => x is null || x.Trim().Length is >= 1 and <= 254);
        v.RuleFor(x => password(x)).Must(x => x is null || x.Length is >= 1 and <= 1024);
        v.RuleFor(x => email(x)).Must(x => x is null || (x.Length <= 254 && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(x.Trim())));
        v.RuleFor(x => name(x)).Must(x => x is null || (x.Trim().Length is >= 1 and <= 200 && x.All(c => !char.IsControl(c))));
    }
    private static bool IsHost(string value) => value.Length <= 253 && (System.Net.IPAddress.TryParse(value, out _) || Uri.CheckHostName(value) == UriHostNameType.Dns);
}
public sealed class CreateSenderRequestValidator : AbstractValidator<CreateSenderRequest>
{
    public CreateSenderRequestValidator()
    {
        RuleFor(x => x.Key).Matches("^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$");
        SenderRules.Apply(this, x => x.Host, x => x.Port, x => x.Username, x => x.Password, x => x.FromEmail, x => x.FromName);
    }
}
public sealed class PatchSenderRequestValidator : AbstractValidator<PatchSenderRequest>
{
    public PatchSenderRequestValidator() => SenderRules.Apply(this, x => x.Host, x => x.Port, x => x.Username, x => x.Password, x => x.FromEmail, x => x.FromName);
}
