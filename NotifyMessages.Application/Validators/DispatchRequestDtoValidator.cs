using System.Text.RegularExpressions;
using FluentValidation;
using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Validators;

public partial class DispatchRequestDtoValidator : AbstractValidator<DispatchRequestDto>
{
    public DispatchRequestDtoValidator()
    {
        RuleFor(x => x.TenantId)
            .GreaterThan(0).WithMessage("TenantId deve ser um identificador válido.");

        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("TemplateId deve ser um identificador válido.");

        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("RecipientName é obrigatório.")
            .MaximumLength(100);

        RuleFor(x => x.RecipientContact)
            .NotEmpty().WithMessage("RecipientContact é obrigatório.")
            .MaximumLength(100)
            .Must(contact => EmailRegex().IsMatch(contact) || PhoneRegex().IsMatch(contact))
            .WithMessage("RecipientContact deve ser um email ou número de telefone válido.");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?[0-9]{7,15}$")]
    private static partial Regex PhoneRegex();
}
