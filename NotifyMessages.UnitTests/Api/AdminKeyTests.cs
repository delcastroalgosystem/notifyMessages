using NotifyMessages.Api.Authentication;
using NotifyMessages.Application.Security;

namespace NotifyMessages.UnitTests.Api;

public class AdminKeyTests
{
    [Fact]
    public void IsValid_ChaveComHashConfigurado_AceitaSemDistinguirMaiusculasDoHash()
    {
        string chave = ApiKeyHasher.GenerateApiKey();
        string hash = ApiKeyHasher.Hash(chave);

        Assert.True(AdminKeyAuthenticationHandler.IsValid(chave, [hash]));
        Assert.True(AdminKeyAuthenticationHandler.IsValid(chave, ["outro", hash.ToLowerInvariant()]));
    }

    [Fact]
    public void IsValid_ChaveErradaOuSemHashes_Recusa()
    {
        string hash = ApiKeyHasher.Hash(ApiKeyHasher.GenerateApiKey());

        Assert.False(AdminKeyAuthenticationHandler.IsValid("errada", [hash]));
        Assert.False(AdminKeyAuthenticationHandler.IsValid("qualquer", []));
        Assert.False(AdminKeyAuthenticationHandler.IsValid("qualquer", ["", "  "]));
    }
}
