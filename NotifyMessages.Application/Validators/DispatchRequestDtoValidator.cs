using FluentValidation;
using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Validators;

public class DispatchRequestDtoValidator : AbstractValidator<DispatchRequestDto>
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
            .Must(ContactRules.IsValid)
            .WithMessage("RecipientContact deve ser um email ou número de telefone válido.");

        RuleFor(x => x.ExternalKey)
            .MaximumLength(150)
            .Must(k => k == null || !string.IsNullOrWhiteSpace(k))
            .WithMessage("ExternalKey, quando enviada, não pode estar vazia.");
    }
}
