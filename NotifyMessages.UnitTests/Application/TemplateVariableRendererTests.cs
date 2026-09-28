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
    public void RenderHtml_EscapaOsValoresMasNaoOTemplate()
    {
        var result = TemplateVariableRenderer.RenderHtml(
            "<p>Olá <b>{{Nome}}</b>, {{Valor}}</p>",
            new Dictionary<string, string> { ["Nome"] = "Silva & Filhos <script>alert(1)</script>", ["Valor"] = "7,50 €" });

        Assert.Equal("<p>Olá <b>Silva &amp; Filhos &lt;script&gt;alert(1)&lt;/script&gt;</b>, 7,50 €</p>", result);
    }

    [Fact]
    public void Render_TextoSimples_NaoEscapa()
    {
        var result = TemplateVariableRenderer.Render("Olá {{Nome}}", new Dictionary<string, string> { ["Nome"] = "Silva & Filhos" });

        Assert.Equal("Olá Silva & Filhos", result);
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
