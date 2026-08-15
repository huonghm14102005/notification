using FluentValidation;

namespace Notification.Api.Contracts.Identity;

public sealed record CreateApiKeyRequest(string ProducerName);

public sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator()
    {
        RuleFor(x => x.ProducerName).NotEmpty().MinimumLength(2).MaximumLength(100)
            .Must(value => value.Trim() == value && value.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c is '.' or '_' or '-'))
            .WithMessage("ProducerName must be trimmed and contain only letters, numbers, spaces, dot, underscore or hyphen.");
    }
}
