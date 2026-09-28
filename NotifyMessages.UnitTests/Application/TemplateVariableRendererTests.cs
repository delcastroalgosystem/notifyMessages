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

    private const string ComMB = "<p>Olá {{Nome}}</p>{{#ReferenciaMB}}<p>Ref. {{ReferenciaMB}} · {{ValorMB}}</p>{{/ReferenciaMB}}{{^ReferenciaMB}}<p>Contacte a secretaria.</p>{{/ReferenciaMB}}";

    [Fact]
    public void Blocos_ComValor_MostraOBlocoPositivo()
    {
        var r = TemplateVariableRenderer.RenderHtml(ComMB,
            new Dictionary<string, string> { ["Nome"] = "Ana", ["ReferenciaMB"] = "737 891 529", ["ValorMB"] = "64,00 €" });

        Assert.Equal("<p>Olá Ana</p><p>Ref. 737 891 529 · 64,00 €</p>", r);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blocos_SemValorOuSemVariavel_MostraOBlocoNegativo(string? referencia)
    {
        var vars = new Dictionary<string, string> { ["Nome"] = "Ana", ["ValorMB"] = "" };
        if (referencia is not null) vars["ReferenciaMB"] = referencia;

        var r = TemplateVariableRenderer.RenderHtml(ComMB, vars);

        Assert.Equal("<p>Olá Ana</p><p>Contacte a secretaria.</p>", r);
    }

    [Fact]
    public void Blocos_EncadeadosEMultilinhaESemDistinguirMaiusculas()
    {
        const string t = "{{#A}}a[{{#b}}b\n{{/B}}]{{/a}}{{^C}}sem c{{/C}}";

        Assert.Equal("a[b\n]sem c", TemplateVariableRenderer.Render(t, new Dictionary<string, string> { ["a"] = "1", ["B"] = "1" }));
        Assert.Equal("a[]sem c", TemplateVariableRenderer.Render(t, new Dictionary<string, string> { ["A"] = "1" }));
        Assert.Equal("", TemplateVariableRenderer.Render(t, new Dictionary<string, string> { ["C"] = "1" }));
    }

    [Fact]
    public void ExtractVariableNames_IncluiAsVariaveisDosBlocos()
    {
        var names = TemplateVariableRenderer.ExtractVariableNames(ComMB);

        Assert.Equal(["Nome", "ReferenciaMB", "ValorMB"], names);
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
