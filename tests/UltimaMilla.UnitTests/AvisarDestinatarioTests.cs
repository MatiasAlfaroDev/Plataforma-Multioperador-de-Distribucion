using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Notificaciones.Commands.AvisarDestinatario;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

/// <summary>RF 27 y RF 5: correo al destinatario en los cambios de estado relevantes, con la marca del operador.</summary>
public class AvisarDestinatarioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly Operador Rapi = Operador.Crear(Guid.NewGuid(), "RapiEnvíos", "#E4572E", "contacto@rapi.uy");

    private readonly CorreoFalso _correo = new();

    private static Envio NuevoEnvio(string? email) => Envio.Crear(
        CuentaComercial.Crear(Guid.NewGuid(), Rapi.Id, comercioId: Guid.NewGuid()),
        "PED-27",
        Modalidad.Estandar,
        new Destinatario("Ana Pérez", null, email),
        new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
        [new NuevoBulto(2.5m, 20, 30, 40)],
        Ahora);

    private AvisarDestinatarioHandler Handler(Envio envio) =>
        new(new EnviosEnMemoria(envio), new OperadoresEnMemoria(Rapi), _correo);

    [Theory]
    [InlineData(EstadoEnvio.EnTransito)]
    [InlineData(EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.EntregadoParcialmente)]
    [InlineData(EstadoEnvio.NoEntregado)]
    [InlineData(EstadoEnvio.Reprogramado)]
    public async Task Los_estados_relevantes_mandan_un_correo(EstadoEnvio estado)
    {
        var envio = NuevoEnvio("ana@correo.uy");

        Assert.True(await Handler(envio).Handle(new AvisarDestinatarioCommand(envio.Id, estado), CancellationToken.None));

        var enviado = Assert.Single(_correo.Enviados);
        Assert.Equal("ana@correo.uy", enviado.Para);
    }

    [Theory]
    [InlineData(EstadoEnvio.Admitido)]
    [InlineData(EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.EnDevolucion)]
    [InlineData(EstadoEnvio.Devuelto)]
    [InlineData(EstadoEnvio.Extraviado)]
    public async Task Los_estados_internos_no_mandan_correo(EstadoEnvio estado)
    {
        var envio = NuevoEnvio("ana@correo.uy");

        Assert.False(await Handler(envio).Handle(new AvisarDestinatarioCommand(envio.Id, estado), CancellationToken.None));
        Assert.Empty(_correo.Enviados);
    }

    [Fact]
    public async Task Sin_correo_del_destinatario_no_se_manda_nada()
    {
        var envio = NuevoEnvio(null);

        Assert.False(await Handler(envio).Handle(new AvisarDestinatarioCommand(envio.Id, EstadoEnvio.Entregado), CancellationToken.None));
        Assert.Empty(_correo.Enviados);
    }

    [Fact]
    public async Task El_correo_lleva_la_marca_del_operador_y_la_referencia()
    {
        var envio = NuevoEnvio("ana@correo.uy");

        await Handler(envio).Handle(new AvisarDestinatarioCommand(envio.Id, EstadoEnvio.NoEntregado), CancellationToken.None);

        var enviado = Assert.Single(_correo.Enviados);
        Assert.Contains("RapiEnvíos", enviado.Asunto);
        Assert.Contains("PED-27", enviado.Asunto);
        Assert.Contains(System.Net.WebUtility.HtmlEncode("RapiEnvíos"), enviado.Cuerpo);
        Assert.Contains("#E4572E", enviado.Cuerpo);
        Assert.Contains("enlace de seguimiento", enviado.Cuerpo);
    }

    // ----- Dobles de prueba -----

    private sealed record Correo(string Para, string Asunto, string Cuerpo);

    private sealed class CorreoFalso : IEnviadorCorreo
    {
        public List<Correo> Enviados { get; } = [];

        public Task EnviarAsync(string para, string asunto, string cuerpoHtml, CancellationToken ct)
        {
            Enviados.Add(new Correo(para, asunto, cuerpoHtml));
            return Task.CompletedTask;
        }
    }

    private sealed class OperadoresEnMemoria(Operador operador) : IOperadorRepository
    {
        public Task<IReadOnlyList<Operador>> ListarAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Operador>>([operador]);

        public Task<Operador?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == operador.Id ? operador : null);
    }

    private sealed class EnviosEnMemoria(Envio envio) : IEnvioRepository
    {
        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == envio.Id ? envio : null);

        public Task AgregarAsync(Envio nuevo, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Envio>> ListarAsync(Guid operadorId, Guid? cuentaComercialId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Envio>> ListarPorCuentasComercialesAsync(IReadOnlyCollection<Guid> cuentasComercialesIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(Guid operadorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Envio?> ObtenerPorTokenSeguimientoAsync(string tokenHash, CancellationToken ct) => throw new NotSupportedException();
        public Task<Envio?> ObtenerPorComercioAsync(Guid envioId, Guid comercioId, CancellationToken ct) => throw new NotSupportedException();
    }
}
