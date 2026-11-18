
using System.Net;
using Xunit;

namespace UltimaMilla.IntegrationTests;

public sealed class IdentidadVisualSeguridadTests
{
    [Fact]
    public async Task SinAutenticacion_NoPuedeConsultarOperador()
    {
        await using var factory = new ApiSeguridadFactory();
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync(
            "/api/operadores/mi-operador");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);
    }

    [Fact]
    public async Task UsuarioSinRolAdministrador_NoPuedeConsultarOperador()
    {
        await using var factory = new ApiSeguridadFactory();
        using var cliente = factory.CreateClient();

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Usuario", Guid.NewGuid().ToString());

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Rol", "Repartidor");

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Operador", Guid.NewGuid().ToString());

        var respuesta = await cliente.GetAsync(
            "/api/operadores/mi-operador");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            respuesta.StatusCode);
    }

    [Fact]
    public async Task AdministradorSinOperador_NoPuedeConsultarConfiguracion()
    {
        await using var factory = new ApiSeguridadFactory();
        using var cliente = factory.CreateClient();

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Usuario", Guid.NewGuid().ToString());

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Rol", "AdministradorOperador");

        var respuesta = await cliente.GetAsync(
            "/api/operadores/mi-operador");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            respuesta.StatusCode);
    }
}
