
using System.Net;
using Xunit;

namespace UltimaMilla.IntegrationTests;

public sealed class ApiArranqueTests
{
    [Fact]
    public async Task ApiTesting_PuedeIniciarYResponder()
    {
        await using var factory = new ApiSeguridadFactory();

        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/live");

        Assert.Equal(
            HttpStatusCode.OK,
            respuesta.StatusCode);
    }
}
