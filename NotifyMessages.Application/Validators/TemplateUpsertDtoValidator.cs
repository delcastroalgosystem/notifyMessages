using FluentValidation;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Validators;

public class TemplateUpsertDtoValidator : AbstractValidator<TemplateUpsertDto>
{
    public TemplateUpsertDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name é obrigatório.")
            .MaximumLength(100);

        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.ProviderType).IsInEnum();

        RuleFor(x => x.Subject)
            .MaximumLength(200)
            .NotEmpty().When(x => x.Channel == ChannelType.Email)
            .WithMessage("Subject é obrigatório para templates de Email.");

        RuleFor(x => x.HtmlBody)
            .NotEmpty().When(x => x.Channel == ChannelType.Email)
            .WithMessage("HtmlBody é obrigatório para templates de Email.");

        RuleFor(x => x.TextBody)
            .NotEmpty().When(x => x.Channel == ChannelType.Sms)
            .WithMessage("TextBody é obrigatório para templates de SMS.");
    }
}
