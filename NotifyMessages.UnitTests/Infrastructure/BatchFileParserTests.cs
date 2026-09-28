using System.Text;
using ClosedXML.Excel;
using NotifyMessages.Infrastructure.Import;

namespace NotifyMessages.UnitTests.Infrastructure;

public class BatchFileParserTests
{
    private static Stream Csv(string texto, Encoding? encoding = null) => new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(texto));

    [Fact]
    public void Csv_PontoEVirgula_ColunasReconhecidasEOutrasViramVariaveis()
    {
        var r = new BatchFileParser().Parse(Csv("E-mail;Nome;Chave;Valor\nana@x.pt;Ana Silva;SOCIO:1;10,00 €\nrui@x.pt;;;5\n"), "lista.csv");

        Assert.Empty(r.Errors);
        Assert.Equal(2, r.Items.Count);
        Assert.Equal(("ana@x.pt", "Ana Silva", "SOCIO:1"), (r.Items[0].RecipientContact, r.Items[0].RecipientName, r.Items[0].ExternalKey));
        Assert.Equal("10,00 €", r.Items[0].BusinessData!["Valor"]);
        // A coluna do nome também chega ao template como {{Nome}}; contacto e chave não
        Assert.Equal("Ana Silva", r.Items[0].BusinessData!["Nome"]);
        Assert.False(r.Items[0].BusinessData!.ContainsKey("E-mail"));
        Assert.False(r.Items[0].BusinessData!.ContainsKey("Chave"));
        // Sem nome: usa o contacto; sem chave: fica vazia (gerada ao criar o lote)
        Assert.Equal(("rui@x.pt", ""), (r.Items[1].RecipientName, r.Items[1].ExternalKey));
    }

    [Fact]
    public void Csv_VirgulaComAspas()
    {
        var r = new BatchFileParser().Parse(Csv("email,nome,morada\nana@x.pt,\"Silva, Ana\",\"Rua \"\"A\"\", 1\"\n"), "l.csv");

        Assert.Equal("Silva, Ana", r.Items[0].RecipientName);
        Assert.Equal("Rua \"A\", 1", r.Items[0].BusinessData!["morada"]);
    }

    [Fact]
    public void Csv_Latin1ExportadoPeloExcel_AcentosCorretos()
    {
        var r = new BatchFileParser().Parse(Csv("Telemóvel;Nome\n+351912345678;João Gonçalves\n", Encoding.Latin1), "l.csv");

        Assert.Equal(("+351912345678", "João Gonçalves"), (r.Items[0].RecipientContact, r.Items[0].RecipientName));
    }

    [Fact]
    public void Csv_SemColunaDeContacto_Erro()
    {
        var r = new BatchFileParser().Parse(Csv("nome;valor\nAna;10\n"), "l.csv");

        Assert.Empty(r.Items);
        Assert.Contains("contacto", Assert.Single(r.Errors));
    }

    [Fact]
    public void FormatoNaoSuportado_Erro()
        => Assert.Contains(".pdf", Assert.Single(new BatchFileParser().Parse(Csv("x"), "lista.pdf").Errors));

    [Fact]
    public void Excel_PrimeiraFolha()
    {
        using var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Sócios");
            ws.Cell(1, 1).Value = "Email"; ws.Cell(1, 2).Value = "Nome"; ws.Cell(1, 3).Value = "Quotas";
            ws.Cell(2, 1).Value = "ana@x.pt"; ws.Cell(2, 2).Value = "Ana"; ws.Cell(2, 3).Value = 3;
            wb.SaveAs(ms);
        }
        ms.Position = 0;

        var r = new BatchFileParser().Parse(ms, "lista.xlsx");

        Assert.Empty(r.Errors);
        var item = Assert.Single(r.Items);
        Assert.Equal(("ana@x.pt", "Ana", "3"), (item.RecipientContact, item.RecipientName, item.BusinessData!["Quotas"]));
    }

    [Fact]
    public void LinhasVaziasSaoIgnoradas()
        => Assert.Single(new BatchFileParser().Parse(Csv("email\n\nana@x.pt\n;\n"), "l.csv").Items);
}
