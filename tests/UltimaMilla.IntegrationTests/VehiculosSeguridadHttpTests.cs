
using System.Net;

namespace UltimaMilla.IntegrationTests;

public class VehiculosSeguridadHttpTests
{
    [Fact]
    public async Task UsuarioSinAutenticar_NoPuedeConsultarVehiculos()
    {
        await using var factory = new ApiSeguridadFactory();

        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/vehiculos");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);
    }

[Fact]
public async Task UsuarioSinRolConfiguracion_NoPuedeConsultarVehiculos()
{
    await using var factory = new ApiSeguridadFactory();

    using var cliente = factory.CreateClient();

    cliente.DefaultRequestHeaders.Add(
        "X-Test-Usuario",
        Guid.NewGuid().ToString());

    cliente.DefaultRequestHeaders.Add(
        "X-Test-Rol",
        "Repartidor");

    cliente.DefaultRequestHeaders.Add(
        "X-Test-Operador",
        "11111111-0000-0000-0000-000000000001");

    var respuesta = await cliente.GetAsync("/api/vehiculos");

    Assert.Equal(
        HttpStatusCode.Forbidden,
        respuesta.StatusCode);
}

}
