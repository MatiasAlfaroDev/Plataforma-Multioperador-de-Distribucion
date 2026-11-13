using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;
using UltimaMilla.Application.Envios.Queries.ListarDiscrepanciasRecepcion;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using Xunit;

namespace UltimaMilla.UnitTests;

/// <summary>RF 10: recepción en depósito con verificación contra lo declarado y registro de discrepancias.</summary>
public sealed class RecibirEnDepositoHandlerTests
{
    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly DateTimeOffset _ahora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly Envio _envio;
    private readonly VersionReglasOperativas _reglas;
    private readonly UnidadDeTrabajo _unidad = new();
    private readonly DiscrepanciasEnMemoria _discrepancias = new();
    private readonly EnviosEnMemoria _envios;

    public RecibirEnDepositoHandlerTests()
    {
        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            _operadorId,
            Guid.NewGuid());

        _reglas = VersionReglasOperativas.Crear(
            _operadorId,
            maximoIntentosEntrega: 1,
            horasMinimasEntreIntentos: 24,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 7,
            vigenciaDesde: _ahora.AddDays(-1));

        _envio = Envio.Crear(
            cuenta,
            $"REC-{Guid.NewGuid():N}",
            Modalidad.Estandar,
            new Destinatario("Destinatario", "099123456"),
            new Direccion("Origen", "100", "San José", "San José"),
            [new NuevoBulto(5m, 20, 20, 20), new NuevoBulto(3m, 10, 10, 10)],
            _ahora);

        _envio.AsignarReglasOperativas(_reglas.Id);
        _envios = new EnviosEnMemoria(_envio);
    }

    private IReadOnlyList<string> Declarados =>
        _envio.Bultos.Select(b => b.Identificacion).ToList();

    private RecibirEnDepositoHandler CrearHandler(
        string perfil = Perfiles.Operador) =>
        new(
            _envios,
            _discrepancias,
            new ReglasEnMemoria(_reglas),
            _unidad,
            new RelojFijo(_ahora),
            new CacheFalsa(),
            new TenantDePrueba(_operadorId, perfil));

    private static RecibirEnDepositoCommand Comando(
        Envio envio,
        IReadOnlyList<string> escaneados) =>
        new(envio.Id, escaneados, "Operario");

    [Fact]
    public async Task EscaneoCompleto_RecibeElEnvioSinDiscrepancias()
    {
        var resultado = await CrearHandler().Handle(
            Comando(_envio, Declarados), CancellationToken.None);

        Assert.True(resultado.Recibido);
        Assert.Empty(resultado.Discrepancias);
        Assert.Empty(_discrepancias.Guardadas);
        Assert.Equal(EstadoEnvio.EnDeposito, _envio.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task EscaneoEnOtraCapitalizacion_NoGeneraDiscrepancia()
    {
        var escaneados = Declarados.Select(c => c.ToLowerInvariant()).ToList();

        var resultado = await CrearHandler().Handle(
            Comando(_envio, escaneados), CancellationToken.None);

        Assert.True(resultado.Recibido);
        Assert.Empty(resultado.Discrepancias);
    }

    [Fact]
    public async Task BultoFaltante_NoRecibeElEnvioPeroRegistraLaDiscrepancia()
    {
        var faltante = Declarados[1];

        var resultado = await CrearHandler().Handle(
            Comando(_envio, [Declarados[0]]), CancellationToken.None);

        Assert.False(resultado.Recibido);
        var dto = Assert.Single(resultado.Discrepancias);
        Assert.Equal(TipoDiscrepanciaRecepcion.BultoFaltante, dto.Tipo);
        Assert.Equal(faltante, dto.Identificacion);
        Assert.Equal("Operario", dto.Responsable);
        Assert.Equal(_ahora, dto.RegistradaEn);

        // El envío sigue Admitido, pero la discrepancia se guardó.
        Assert.Equal(EstadoEnvio.Admitido, _envio.Estado);
        var guardada = Assert.Single(_discrepancias.Guardadas);
        Assert.Equal(_envio.Id, guardada.EnvioId);
        Assert.Equal(_operadorId, guardada.OperadorId);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task BultoNoDeclarado_RecibeElEnvioYRegistraLaDiscrepancia()
    {
        var resultado = await CrearHandler().Handle(
            Comando(_envio, [.. Declarados, "EXTRA-999"]),
            CancellationToken.None);

        Assert.True(resultado.Recibido);
        var dto = Assert.Single(resultado.Discrepancias);
        Assert.Equal(TipoDiscrepanciaRecepcion.BultoNoDeclarado, dto.Tipo);
        Assert.Equal("EXTRA-999", dto.Identificacion);

        Assert.Equal(EstadoEnvio.EnDeposito, _envio.Estado);
        Assert.Single(_discrepancias.Guardadas);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task FaltanteYNoDeclarado_RegistraAmbasYNoRecibe()
    {
        var resultado = await CrearHandler().Handle(
            Comando(_envio, [Declarados[0], "EXTRA-999"]),
            CancellationToken.None);

        Assert.False(resultado.Recibido);
        Assert.Equal(2, resultado.Discrepancias.Count);
        Assert.Contains(resultado.Discrepancias,
            d => d.Tipo == TipoDiscrepanciaRecepcion.BultoFaltante);
        Assert.Contains(resultado.Discrepancias,
            d => d.Tipo == TipoDiscrepanciaRecepcion.BultoNoDeclarado);
        Assert.Equal(EstadoEnvio.Admitido, _envio.Estado);
        Assert.Equal(2, _discrepancias.Guardadas.Count);
    }

    [Fact]
    public async Task IdentificacionesRepetidas_SeRechazanSinRegistrarNada()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearHandler().Handle(
                Comando(_envio, [Declarados[0], Declarados[0]]),
                CancellationToken.None));

        Assert.Empty(_discrepancias.Guardadas);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task EnvioYaRecibido_SeRechaza()
    {
        var handler = CrearHandler();
        await handler.Handle(Comando(_envio, Declarados), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                Comando(_envio, Declarados), CancellationToken.None));
    }

    [Fact]
    public async Task PerfilSinPermisos_NoRegistraNiGuarda()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CrearHandler(Perfiles.Comercio).Handle(
                Comando(_envio, [Declarados[0]]), CancellationToken.None));

        Assert.Empty(_discrepancias.Guardadas);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task EnvioInexistente_DevuelveNoEncontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(
            () => CrearHandler().Handle(
                new RecibirEnDepositoCommand(
                    Guid.NewGuid(), Declarados, "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task ListarDiscrepancias_DevuelveLasRegistradasDelEnvio()
    {
        await CrearHandler().Handle(
            Comando(_envio, [Declarados[0]]), CancellationToken.None);

        var lista = await new ListarDiscrepanciasRecepcionHandler(
                _envios, _discrepancias)
            .Handle(_envio.Id, CancellationToken.None);

        var dto = Assert.Single(lista);
        Assert.Equal(TipoDiscrepanciaRecepcion.BultoFaltante, dto.Tipo);
        Assert.Equal(Declarados[1], dto.Identificacion);
    }

    [Fact]
    public async Task ListarDiscrepancias_EnvioInexistente_DevuelveNoEncontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(
            () => new ListarDiscrepanciasRecepcionHandler(
                    _envios, _discrepancias)
                .Handle(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public void Registrar_CopiaOperadorYEnvioYLimpiaEspacios()
    {
        var d = DiscrepanciaRecepcion.Registrar(
            _envio,
            TipoDiscrepanciaRecepcion.BultoFaltante,
            "  B-1  ",
            "  Operario  ",
            _ahora);

        Assert.Equal(_envio.Id, d.EnvioId);
        Assert.Equal(_envio.OperadorId, d.OperadorId);
        Assert.Equal("B-1", d.Identificacion);
        Assert.Equal("Operario", d.Responsable);
        Assert.Equal(_ahora, d.RegistradaEn);
    }

    [Theory]
    [InlineData("", "Operario")]
    [InlineData("  ", "Operario")]
    [InlineData("B-1", "")]
    [InlineData("B-1", "  ")]
    public void Registrar_ConDatosVacios_Falla(string identificacion, string responsable)
    {
        Assert.Throws<ArgumentException>(() =>
            DiscrepanciaRecepcion.Registrar(
                _envio,
                TipoDiscrepanciaRecepcion.BultoFaltante,
                identificacion,
                responsable,
                _ahora));
    }

    [Fact]
    public void Registrar_ConTipoInvalido_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            DiscrepanciaRecepcion.Registrar(
                _envio,
                (TipoDiscrepanciaRecepcion)99,
                "B-1",
                "Operario",
                _ahora));
    }

    private sealed class DiscrepanciasEnMemoria : IDiscrepanciaRecepcionRepository
    {
        public List<DiscrepanciaRecepcion> Guardadas { get; } = [];

        public Task AgregarAsync(
            DiscrepanciaRecepcion discrepancia, CancellationToken ct)
        {
            Guardadas.Add(discrepancia);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DiscrepanciaRecepcion>> ListarPorEnvioAsync(
            Guid envioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DiscrepanciaRecepcion>>(
                Guardadas.Where(d => d.EnvioId == envioId).ToList());
    }

    private sealed class EnviosEnMemoria(Envio envio) : IEnvioRepository
    {
        public Task AgregarAsync(Envio valor, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Envio?>(id == envio.Id ? envio : null);

        public Task<bool> ExisteReferenciaAsync(
            Guid cuentaId, string referencia, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Envio>> ListarAsync(
            Guid operadorId, Guid? cuentaId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task<IReadOnlyList<Envio>> ListarPorCuentasComercialesAsync(
            IReadOnlyCollection<Guid> cuentasIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(
            Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task<Envio?> ObtenerPorTokenSeguimientoAsync(
            string token, CancellationToken ct) =>
            Task.FromResult<Envio?>(null);

        public Task<Envio?> ObtenerPorComercioAsync(
            Guid envioId, Guid comercioId, CancellationToken ct) =>
            Task.FromResult<Envio?>(envio.Id == envioId ? envio : null);
    }

    private sealed class ReglasEnMemoria(VersionReglasOperativas reglas)
        : IVersionReglasOperativasRepository
    {
        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
            Guid operadorId, Guid versionId, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(
                reglas.OperadorId == operadorId &&
                reglas.Id == versionId ? reglas : null);

        public Task<VersionReglasOperativas?> ObtenerVigenteAsync(
            Guid operadorId, DateTimeOffset fecha, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(reglas);

        public Task<VersionReglasOperativas?> ObtenerVigenteParaActualizarAsync(
            Guid operadorId, DateTimeOffset fecha, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(reglas);

        public Task AgregarAsync(
            VersionReglasOperativas version, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(
            Func<CancellationToken, Task> operacion, CancellationToken ct) =>
            operacion(ct);
    }

    private sealed class TenantDePrueba(Guid operadorId, string perfil) : ITenantContext
    {
        public Guid? UsuarioId => Guid.Parse("00000000-0000-0000-0000-0000000000aa");
        public Guid? ComercioId => null;
        public Guid? OperadorId => operadorId;
        public string? Perfil => perfil;
        public bool EstaAutenticado => true;
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class CacheFalsa : ICacheService
    {
        public Task<T?> ObtenerAsync<T>(
            string clave, CancellationToken ct = default) =>
            Task.FromResult<T?>(default);

        public Task GuardarAsync<T>(
            string clave, T valor, TimeSpan duracion,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EliminarAsync(
            string clave, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
