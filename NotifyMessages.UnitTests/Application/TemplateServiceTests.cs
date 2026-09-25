using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class TemplateServiceTests
{
    private static TemplateUpsertDto BuildEmailUpsert(string name = "Boas-Vindas") => new()
    {
        Name = name,
        Channel = ChannelType.Email,
        ProviderType = ProviderType.SendGrid,
        Subject = "Olá {{Nome}}",
        HtmlBody = "<p>Olá {{Nome}}, o valor é {{Valor}}</p>",
        TextBody = "Olá {{Nome}}, o valor é {{Valor}}",
        IsActive = true
    };

    [Fact]
    public async Task CreateAsync_CriaTemplateEDevolveDtoComVariaveisExtraidas()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var created = await service.CreateAsync(BuildEmailUpsert());

        Assert.True(created.Id > 0);
        Assert.Contains("Nome", created.Variables);
        Assert.Contains("Valor", created.Variables);
        Assert.Single(context.Templates);
    }

    [Fact]
    public async Task GetAllAsync_FiltraPorIsActive()
    {
        using var context = TestDbContextFactory.Create();
        context.Templates.Add(new Template { Name = "Ativo", Channel = ChannelType.Sms, ProviderType = ProviderType.Twilio, TextBody = "x", IsActive = true });
        context.Templates.Add(new Template { Name = "Inativo", Channel = ChannelType.Sms, ProviderType = ProviderType.Twilio, TextBody = "x", IsActive = false });
        context.SaveChanges();

        var service = new TemplateService(context);

        var ativos = await service.GetAllAsync(isActive: true);
        var inativos = await service.GetAllAsync(isActive: false);
        var todos = await service.GetAllAsync();

        Assert.Single(ativos);
        Assert.Single(inativos);
        Assert.Equal(2, todos.Count);
    }

    [Fact]
    public async Task GetByIdAsync_IdInexistente_DevolveNull()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var result = await service.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_AtualizaCamposEDefineUpdatedAt()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);
        var created = await service.CreateAsync(BuildEmailUpsert());

        var update = BuildEmailUpsert("Boas-Vindas Editado");
        var updated = await service.UpdateAsync(created.Id, update);

        Assert.NotNull(updated);
        Assert.Equal("Boas-Vindas Editado", updated!.Name);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_IdInexistente_DevolveNull()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var result = await service.UpdateAsync(999, BuildEmailUpsert());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeactivateAsync_MarcaComoInativo()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);
        var created = await service.CreateAsync(BuildEmailUpsert());

        var success = await service.DeactivateAsync(created.Id);
        var reloaded = await service.GetByIdAsync(created.Id);

        Assert.True(success);
        Assert.False(reloaded!.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_IdInexistente_DevolveFalse()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var success = await service.DeactivateAsync(999);

        Assert.False(success);
    }

    [Fact]
    public async Task PreviewAsync_SubstituiVariaveisFornecidasEListaFaltantes()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);
        var created = await service.CreateAsync(BuildEmailUpsert());

        var preview = await service.PreviewAsync(created.Id, new TemplatePreviewRequestDto
        {
            Variables = new Dictionary<string, string> { ["Nome"] = "Wendel" }
        });

        Assert.NotNull(preview);
        Assert.Equal("Olá Wendel", preview!.Subject);
        Assert.Contains("Wendel", preview.HtmlBody);
        Assert.Contains("{{Valor}}", preview.HtmlBody);
        Assert.Contains("Valor", preview.MissingVariables);
        Assert.DoesNotContain("Nome", preview.MissingVariables);
    }

    [Fact]
    public async Task PreviewAsync_IdInexistente_DevolveNull()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var preview = await service.PreviewAsync(999, new TemplatePreviewRequestDto());

        Assert.Null(preview);
    }
}
