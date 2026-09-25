using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class TemplateServiceTests
{
    private const int Tenant = 1;

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

        var created = await service.CreateAsync(Tenant, BuildEmailUpsert());

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

        var ativos = await service.GetAllAsync(Tenant, isActive: true);
        var inativos = await service.GetAllAsync(Tenant, isActive: false);
        var todos = await service.GetAllAsync(Tenant);

        Assert.Single(ativos);
        Assert.Single(inativos);
        Assert.Equal(2, todos.Count);
    }

    [Fact]
    public async Task GetByIdAsync_IdInexistente_DevolveNull()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var result = await service.GetByIdAsync(Tenant, 999);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_AtualizaCamposEDefineUpdatedAt()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);
        var created = await service.CreateAsync(Tenant, BuildEmailUpsert());

        var update = BuildEmailUpsert("Boas-Vindas Editado");
        var updated = await service.UpdateAsync(Tenant, created.Id, update);

        Assert.NotNull(updated);
        Assert.Equal("Boas-Vindas Editado", updated!.Name);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_IdInexistente_DevolveNull()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var result = await service.UpdateAsync(Tenant, 999, BuildEmailUpsert());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeactivateAsync_MarcaComoInativo()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);
        var created = await service.CreateAsync(Tenant, BuildEmailUpsert());

        var success = await service.DeactivateAsync(Tenant, created.Id);
        var reloaded = await service.GetByIdAsync(Tenant, created.Id);

        Assert.True(success);
        Assert.False(reloaded!.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_IdInexistente_DevolveFalse()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var success = await service.DeactivateAsync(Tenant, 999);

        Assert.False(success);
    }

    [Fact]
    public async Task PreviewAsync_SubstituiVariaveisFornecidasEListaFaltantes()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);
        var created = await service.CreateAsync(Tenant, BuildEmailUpsert());

        var preview = await service.PreviewAsync(Tenant, created.Id, new TemplatePreviewRequestDto
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
    public async Task CreateAsync_FicaDoTenantQueCria()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var created = await service.CreateAsync(Tenant, BuildEmailUpsert());

        Assert.Equal(Tenant, created.TenantId);
        Assert.Equal(Tenant, Assert.Single(context.Templates).TenantId);
    }

    [Fact]
    public async Task GetAllAsync_TenantVeOsSeusEOsPartilhados_NaoOsDeOutro()
    {
        using var context = TestDbContextFactory.Create();
        context.Templates.Add(new Template { Name = "Meu", TenantId = Tenant, Channel = ChannelType.Sms, ProviderType = ProviderType.Twilio, TextBody = "x" });
        context.Templates.Add(new Template { Name = "Partilhado", TenantId = null, Channel = ChannelType.Sms, ProviderType = ProviderType.Twilio, TextBody = "x" });
        context.Templates.Add(new Template { Name = "De outro", TenantId = 2, Channel = ChannelType.Sms, ProviderType = ProviderType.Twilio, TextBody = "x" });
        context.SaveChanges();
        var service = new TemplateService(context);

        var visiveis = await service.GetAllAsync(Tenant);
        var deOutro = context.Templates.Single(t => t.Name == "De outro");

        Assert.Equal(["Meu", "Partilhado"], visiveis.Select(t => t.Name).OrderBy(n => n));
        Assert.Null(await service.GetByIdAsync(Tenant, deOutro.Id));
        Assert.Equal(3, (await service.GetAllAsync(tenantId: null)).Count);
    }

    [Fact]
    public async Task UpdateEDeactivate_TenantNaoAlteraPartilhadoNemDeOutro()
    {
        using var context = TestDbContextFactory.Create();
        context.Templates.Add(new Template { Id = 10, Name = "Partilhado", TenantId = null, Channel = ChannelType.Sms, ProviderType = ProviderType.Twilio, TextBody = "x", IsActive = true });
        context.Templates.Add(new Template { Id = 11, Name = "De outro", TenantId = 2, Channel = ChannelType.Sms, ProviderType = ProviderType.Twilio, TextBody = "x", IsActive = true });
        context.SaveChanges();
        var service = new TemplateService(context);

        Assert.Null(await service.UpdateAsync(Tenant, 10, BuildEmailUpsert("Alterado")));
        Assert.Null(await service.UpdateAsync(Tenant, 11, BuildEmailUpsert("Alterado")));
        Assert.False(await service.DeactivateAsync(Tenant, 10));
        Assert.False(await service.DeactivateAsync(Tenant, 11));
        Assert.All(context.Templates, t => Assert.True(t.IsActive));
    }

    [Fact]
    public async Task PreviewAsync_IdInexistente_DevolveNull()
    {
        using var context = TestDbContextFactory.Create();
        var service = new TemplateService(context);

        var preview = await service.PreviewAsync(Tenant, 999, new TemplatePreviewRequestDto());

        Assert.Null(preview);
    }
}
