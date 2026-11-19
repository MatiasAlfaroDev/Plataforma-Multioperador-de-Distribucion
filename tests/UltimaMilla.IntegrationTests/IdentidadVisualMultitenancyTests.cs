
using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Organizacion;
using Xunit;

namespace UltimaMilla.IntegrationTests;

public sealed class IdentidadVisualMultitenancyTests
{
    private static readonly Guid OperadorAId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid OperadorBId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static HttpClient CrearCliente(
        ApiSeguridadFactory factory,
        Guid operadorId)
    {
        var cliente = factory.CreateClient();

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Usuario", Guid.NewGuid().ToString());

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Rol", "AdministradorOperador");

        cliente.DefaultRequestHeaders.Add(
            "X-Test-Operador", operadorId.ToString());

        return cliente;
    }

    // El PUT exige el token antifalsificación; el cliente de prueba guarda la cookie que lo acompaña.
    private static async Task<HttpResponseMessage> CambiarColorAsync(HttpClient cliente, string color)
    {
        var token = await cliente.GetFromJsonAsync<TokenAntiforgery>("/api/antiforgery/token");

        using var solicitud = new HttpRequestMessage(HttpMethod.Put, "/api/operadores/mi-operador/color")
        {
            Content = JsonContent.Create(new { ColorPrimario = color })
        };
        solicitud.Headers.Add("X-CSRF-TOKEN", token!.Token);

        return await cliente.SendAsync(solicitud);
    }

    private sealed record TokenAntiforgery(string Token);

    private static (
        ApiSeguridadFactory Factory,
        IOperadorRepository Repositorio,
        IUnitOfWork Unidad,
        Operador OperadorA,
        Operador OperadorB)
        PrepararEscenario()
    {
        var operadorA = Operador.Crear(
            OperadorAId,
            "Operador A",
            "#2563EB",
            "a@ejemplo.com");

        var operadorB = Operador.Crear(
            OperadorBId,
            "Operador B",
            "#16A34A",
            "b@ejemplo.com");

        var repositorio =
            Substitute.For<IOperadorRepository>();

        var unidad =
            Substitute.For<IUnitOfWork>();

        repositorio.ObtenerAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(info =>
            {
                var id = info.ArgAt<Guid>(0);

                return id == OperadorAId
                    ? operadorA
                    : id == OperadorBId
                        ? operadorB
                        : null;
            });

        repositorio.ObtenerParaActualizarAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(info =>
            {
                var id = info.ArgAt<Guid>(0);

                return id == OperadorAId
                    ? operadorA
                    : id == OperadorBId
                        ? operadorB
                        : null;
            });

        unidad.GuardarCambiosAsync(
                Arg.Any<CancellationToken>())
            .Returns(1);

        var factory = new ApiSeguridadFactory
        {
            RepositorioOperadores = repositorio,
            UnidadDeTrabajo = unidad
        };

        return (
            factory,
            repositorio,
            unidad,
            operadorA,
            operadorB);
    }

    [Fact]
    public async Task AdministradorA_ConsultaSoloSuOperador()
    {
        var escenario = PrepararEscenario();

        await using var factory = escenario.Factory;
        using var cliente = CrearCliente(factory, OperadorAId);

        var respuesta = await cliente.GetAsync(
            "/api/operadores/mi-operador");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var resultado =
            await respuesta.Content.ReadFromJsonAsync<OperadorRespuesta>();

        Assert.NotNull(resultado);
        Assert.Equal(OperadorAId, resultado.Id);
        Assert.Equal("Operador A", resultado.Nombre);
        Assert.Equal("#2563EB", resultado.ColorPrimario);

        await escenario.Repositorio.Received(1)
            .ObtenerAsync(
                OperadorAId,
                Arg.Any<CancellationToken>());

        await escenario.Repositorio.DidNotReceive()
            .ObtenerAsync(
                OperadorBId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdministradorB_ConsultaSoloSuOperador()
    {
        var escenario = PrepararEscenario();

        await using var factory = escenario.Factory;
        using var cliente = CrearCliente(factory, OperadorBId);

        var respuesta = await cliente.GetAsync(
            "/api/operadores/mi-operador");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var resultado =
            await respuesta.Content.ReadFromJsonAsync<OperadorRespuesta>();

        Assert.NotNull(resultado);
        Assert.Equal(OperadorBId, resultado.Id);
        Assert.Equal("Operador B", resultado.Nombre);
        Assert.Equal("#16A34A", resultado.ColorPrimario);
    }

    [Fact]
    public async Task AdministradorA_CambiaSuColor_SinModificarOperadorB()
    {
        var escenario = PrepararEscenario();

        await using var factory = escenario.Factory;
        using var cliente = CrearCliente(factory, OperadorAId);

        var respuesta = await CambiarColorAsync(cliente, "#FF5733");

        Assert.Equal(
            HttpStatusCode.NoContent,
            respuesta.StatusCode);

        Assert.Equal(
            "#FF5733",
            escenario.OperadorA.ColorPrimario);

        Assert.Equal(
            "#16A34A",
            escenario.OperadorB.ColorPrimario);

        await escenario.Repositorio.Received(1)
            .ObtenerParaActualizarAsync(
                OperadorAId,
                Arg.Any<CancellationToken>());

        await escenario.Repositorio.DidNotReceive()
            .ObtenerParaActualizarAsync(
                OperadorBId,
                Arg.Any<CancellationToken>());

        await escenario.Unidad.Received(1)
            .GuardarCambiosAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarColor_SinTokenAntifalsificacion_EsRechazado()
    {
        var escenario = PrepararEscenario();

        await using var factory = escenario.Factory;
        using var cliente = CrearCliente(factory, OperadorAId);

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/operadores/mi-operador/color",
            new { ColorPrimario = "#FF5733" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("#2563EB", escenario.OperadorA.ColorPrimario);
    }

    [Fact]
    public async Task ColorInvalido_DevuelveBadRequest()
    {
        var escenario = PrepararEscenario();

        await using var factory = escenario.Factory;
        using var cliente = CrearCliente(factory, OperadorAId);

        var respuesta = await CambiarColorAsync(cliente, "rojo");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            respuesta.StatusCode);

        Assert.Equal(
            "#2563EB",
            escenario.OperadorA.ColorPrimario);

        await escenario.Unidad.DidNotReceive()
            .GuardarCambiosAsync(
                Arg.Any<CancellationToken>());
    }

    private sealed record OperadorRespuesta(
        Guid Id,
        string Nombre,
        string ColorPrimario);
}
