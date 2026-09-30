using NotifyMessages.Api.Webhooks;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Api;

public class EgoiWebhookParserTests
{
    [Fact]
    public void Parse_FormatoDaDocumentacao_LeCustomDataEMessageIdDeData()
    {
        // Exemplo "Bounce" da documentação E-goi (Webhooks > Email Hooks), com o customData do envio
        const string corpo = """
            [{ "action": "BOUNCE", "actionDate": 1442559422000,
               "data": { "groupId": 1, "subscriberId": 3, "messageId": 1693, "createDate": 1442947084995,
                         "subscriberEmail": "john.doe@example.com", "bounceError": "unknown", "bounceType": "hard",
                         "customData": "4521" } }]
            """;

        var evento = Assert.Single(EgoiWebhookParser.Parse(corpo));

        Assert.Equal(4521, evento.DispatchId);
        Assert.Equal("1693", evento.ExternalId);
        Assert.Equal("bounce", evento.EventType);
        Assert.Equal(DispatchStatus.Bounced, evento.MappedStatus);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1442559422000).UtcDateTime, evento.EventDate);
        Assert.Contains("\"bounceType\": \"hard\"", evento.RawPayload);
    }

    [Fact]
    public void Parse_VariosEventosSemCustomData_UsaMessageIdComoCorrelacao()
    {
        const string corpo = """
            [{ "action": "PROCESSED", "actionDate": 1442484210599, "data": { "messageId": 2156 } },
             { "action": "SENT", "actionDate": 1442947330005, "data": { "messageId": "2156" } }]
            """;

        var eventos = EgoiWebhookParser.Parse(corpo);

        Assert.Equal(2, eventos.Count);
        Assert.All(eventos, e => { Assert.Null(e.DispatchId); Assert.Equal("2156", e.ExternalId); });
        Assert.Null(eventos[0].MappedStatus);
        Assert.Equal(DispatchStatus.Delivered, eventos[1].MappedStatus);
    }

    [Fact]
    public void Parse_ObjetoUnicoNoNivelDeCima_Aceite()
    {
        var evento = Assert.Single(EgoiWebhookParser.Parse("""{ "action": "view", "customData": "7", "message_id": "abc" }"""));

        Assert.Equal(7, evento.DispatchId);
        Assert.Equal("abc", evento.ExternalId);
        Assert.Equal(DispatchStatus.Read, evento.MappedStatus);
    }

    [Theory]
    [InlineData("SENT", DispatchStatus.Delivered)]
    [InlineData("VIEW", DispatchStatus.Read)]
    [InlineData("CLICK", DispatchStatus.Read)]
    [InlineData("BOUNCE", DispatchStatus.Bounced)]
    [InlineData("ABUSE", DispatchStatus.Bounced)]
    [InlineData("FAILED", DispatchStatus.Failed)]
    [InlineData("CANCELED", DispatchStatus.Failed)]
    [InlineData("hard_bounce", DispatchStatus.Bounced)]
    public void MapAction_AcoesDaEgoi(string acao, DispatchStatus esperado)
        => Assert.Equal(esperado, EgoiWebhookParser.MapAction(acao));

    [Theory]
    [InlineData("PROCESSED")]
    [InlineData("REMOVE")]
    [InlineData("desconhecida")]
    public void MapAction_SoRegista(string acao) => Assert.Null(EgoiWebhookParser.MapAction(acao));
}
