using NotifyMessages.Application.Services;

namespace NotifyMessages.UnitTests.Application;

public class TemplateVariableRendererTests
{
    [Fact]
    public void Render_SubstituiVariaveisIgnorandoCase()
    {
        var result = TemplateVariableRenderer.Render(
            "Olá {{nome}}, você tem {{VALOR}} pendente",
            new Dictionary<string, string> { ["Nome"] = "Wendel", ["Valor"] = "10€" });

        Assert.Equal("Olá Wendel, você tem 10€ pendente", result);
    }

    [Fact]
    public void Render_TemplateNulo_DevolveVazio()
    {
        var result = TemplateVariableRenderer.Render(null, new Dictionary<string, string>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractVariableNames_DeduplicaEIgnoraCase()
    {
        var names = TemplateVariableRenderer.ExtractVariableNames("{{Nome}} {{nome}} {{Valor}}", null, "{{Nome}}");

        Assert.Equal(2, names.Count);
        Assert.Contains("Nome", names);
        Assert.Contains("Valor", names);
    }
}
