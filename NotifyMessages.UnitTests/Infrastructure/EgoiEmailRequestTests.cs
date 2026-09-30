using System.Text.Json;
using NotifyMessages.Infrastructure.Providers.Egoi.Models;

namespace NotifyMessages.UnitTests.Infrastructure;

public class EgoiEmailRequestTests
{
    [Fact]
    public void CustomData_VaiNoPedidoQuandoPreenchido()
    {
        string json = JsonSerializer.Serialize(new EgoiEmailRequest { CustomData = "4521" });

        Assert.Contains("\"customData\":\"4521\"", json);
    }

    [Fact]
    public void CustomData_OmitidoQuandoVazio()
    {
        string json = JsonSerializer.Serialize(new EgoiEmailRequest());

        Assert.DoesNotContain("customData", json);
    }
}
