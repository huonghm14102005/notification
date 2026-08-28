using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;

namespace Notification.Api.Contracts.Devices;

public sealed class CreateDeviceRequest
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    [JsonExtensionData] public IDictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class RenameDeviceRequest
{
    public string Name { get; init; } = string.Empty;
    [JsonExtensionData] public IDictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class ConfigureDeviceCallbackRequest
{
    public string Url { get; init; } = string.Empty;
    [JsonExtensionData] public IDictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class RegisterPushEndpointRequest
{
    public string Platform { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    [JsonExtensionData] public IDictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class CreateDeviceValidator : AbstractValidator<CreateDeviceRequest>
{
    public CreateDeviceValidator()
    {
        RuleFor(x => x.Name).Must(DeviceName.IsValid).WithMessage("Name must be trimmed, contain 2 to 100 valid characters and no control characters.");
        RuleFor(x => x.Role).Must(x => x is "source" or "both" or "recipient");
        RuleFor(x => x.Extra).Must(x => x is null || x.Count == 0).WithMessage("Unknown fields are not allowed.");
    }
}

public sealed class RenameDeviceValidator : AbstractValidator<RenameDeviceRequest>
{
    public RenameDeviceValidator()
    {
        RuleFor(x => x.Name).Must(DeviceName.IsValid).WithMessage("Name must be trimmed, contain 2 to 100 valid characters and no control characters.");
        RuleFor(x => x.Extra).Must(x => x is null || x.Count == 0).WithMessage("Unknown fields are not allowed.");
    }
}

public sealed class ConfigureDeviceCallbackValidator : AbstractValidator<ConfigureDeviceCallbackRequest>
{
    public ConfigureDeviceCallbackValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Extra).Must(x => x is null || x.Count == 0).WithMessage("Unknown fields are not allowed.");
    }
}

public sealed class RegisterPushEndpointValidator : AbstractValidator<RegisterPushEndpointRequest>
{
    public RegisterPushEndpointValidator()
    {
        RuleFor(x => x.Platform).NotEmpty().Must(x => x is not null && (x.Trim().ToLowerInvariant() == "fcm" || x.Trim().ToLowerInvariant() == "apns"))
            .WithMessage("Platform must be 'fcm' or 'apns'.");
        RuleFor(x => x.Token).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.Extra).Must(x => x is null || x.Count == 0).WithMessage("Unknown fields are not allowed.");
    }
}

internal static class DeviceName
{
    public static bool IsValid(string? value) => value is not null && value == value.Trim() && value.Length is >= 2 and <= 100 && value.All(c => !char.IsControl(c) && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c is '.' or '_' or '-'));
}
