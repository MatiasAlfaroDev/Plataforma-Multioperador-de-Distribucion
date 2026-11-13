using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.IniciarDevolucion;
using UltimaMilla.Application.Envios.Commands.RetornarADeposito;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using Xunit;

namespace UltimaMilla.UnitTests;

/// <summary>Retorno al depósito de un envío no entregado e inicio de su devolución al comercio.</summary>
public sealed class RetornoYDevolucionHandlersTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly VersionReglasOperativas _reglas;
    private readonly CuentaComercial _cuenta;
    private readonly UnidadDeTrabajo _unidad = new();
    private readonly CacheRegistradora _cache = new();

    public RetornoYDevolucionHandlersTests()
    {
        _cuenta = CuentaComercial.Crear(Guid.NewGuid(), _operadorId, Guid.NewGuid());

        _reglas = VersionReglasOperativas.Crear(
            _operadorId,
            maximoIntentosEntrega: 3,
            horasMinimasEntreIntentos: 24,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 7,
            vigenciaDesde: Ahora.AddDays(-1));
    }

    private Envio NuevoEnvio(string referencia)
    {
        var envio = Envio.Crear(
            _cuenta,
            referencia,
            Modalidad.Estandar,
            new Destinatario("Ana Pérez", "099123456"),
            new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10), new NuevoBulto(2m, 10, 10, 10)],
            Ahora);

        envio.AsignarReglasOperativas(_reglas.Id);
        return envio;
    }

    private void Aplicar(Envio envio, DisparadorEnvio disparador) =>
        envio.Aplicar(disparador, _reglas, Ahora, "Prueba", "Operario");

    /// <summary>Envío que salió a ruta y no se pudo entregar.</summary>
    private Envio EnvioNoEntregado(string referencia, bool motivoRequiereDevolucion = false)
    {
        var envio = NuevoEnvio(referencia);

        Aplicar(envio, DisparadorEnvio.RecibirEnDeposito);
        Aplicar(envio, DisparadorEnvio.AsignarARuta);
        Aplicar(envio, DisparadorEnvio.Despachar);

        var motivo = MotivoNoEntrega.Crear(
            _operadorId, _reglas.Id, "AUSENTE", "Destinatario ausente", motivoRequiereDevolucion);

        envio.Aplicar(
            DisparadorEnvio.RegistrarIntentoFallido,
            _reglas,
            Ahora,
            "Prueba",
            "Repartidor",
            motivoNoEntrega: motivo,
            evidenciaFallo: "foto-fallo");

        return envio;
    }

    /// <summary>Envío no entregado que ya volvió al depósito.</summary>
    private Envio EnvioEnDepositoTrasIntento(string referencia)
    {
        var envio = EnvioNoEntregado(referencia, motivoRequiereDevolucion: true);
        Aplicar(envio, DisparadorEnvio.RetornarADeposito);
        return envio;
    }

    private RetornarADepositoHandler HandlerRetorno(
        Envio? envio,
        string perfil = Perfiles.Operador,
        Guid? operadorId = null) =>
        new(
            new EnviosEnMemoria(envio),
            new ReglasEnMemoria(_reglas),
            _unidad,
            new RelojFijo(Ahora),
            new TenantDePrueba(_usuarioId, operadorId ?? _operadorId, perfil));

    private IniciarDevolucionHandler HandlerDevolucion(
        Envio? envio,
        string perfil = Perfiles.Operador,
        Guid? operadorId = null,
        bool autenticado = true,
        VersionReglasOperativas? reglas = null) =>
        new(
            new EnviosEnMemoria(envio),
            new ReglasEnMemoria(reglas),
            _unidad,
            new TenantDePrueba(_usuarioId, operadorId ?? _operadorId, perfil, autenticado),
            new RelojFijo(Ahora),
            _cache);

    private static IReadOnlyList<string> Codigos(Envio envio) =>
        envio.Bultos.Select(b => b.Identificacion).ToList();

    // ----- RetornarADeposito -----

    [Fact]
    public async Task Retorno_ConTodosLosBultos_VuelveAlDeposito()
    {
        var envio = EnvioNoEntregado("RET-1");

        await HandlerRetorno(envio).Handle(
            new RetornarADepositoCommand(envio.Id, Codigos(envio), " Operario "),
            CancellationToken.None);

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        Assert.Equal(1, _unidad.Guardados);

        var evento = envio.Eventos.Last();
        Assert.Equal(DisparadorEnvio.RetornarADeposito, evento.Disparador);
        Assert.Equal("Deposito", evento.Origen);
        Assert.Equal("Operario", evento.Responsable);
    }

    [Fact]
    public async Task Retorno_ConUnBultoDeMenos_SeRechaza()
    {
        var envio = EnvioNoEntregado("RET-2");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandlerRetorno(envio).Handle(
                new RetornarADepositoCommand(envio.Id, [Codigos(envio)[0]], "Operario"),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.NoEntregado, envio.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Retorno_ConIdentificacionesRepetidas_SeRechaza()
    {
        var envio = EnvioNoEntregado("RET-3");
        var codigo = Codigos(envio)[0];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandlerRetorno(envio).Handle(
                new RetornarADepositoCommand(envio.Id, [codigo, codigo], "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Retorno_ConIdentificacionVacia_SeRechaza()
    {
        var envio = EnvioNoEntregado("RET-4");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            HandlerRetorno(envio).Handle(
                new RetornarADepositoCommand(envio.Id, [Codigos(envio)[0], "  "], "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Retorno_SiElEnvioNoEstaNoEntregado_SeRechaza()
    {
        var envio = NuevoEnvio("RET-5");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandlerRetorno(envio).Handle(
                new RetornarADepositoCommand(envio.Id, Codigos(envio), "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Retorno_SinPermisos_SeRechaza()
    {
        var envio = EnvioNoEntregado("RET-6");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            HandlerRetorno(envio, Perfiles.Comercio).Handle(
                new RetornarADepositoCommand(envio.Id, Codigos(envio), "Operario"),
                CancellationToken.None));

        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Retorno_DeUnEnvioDeOtroOperador_SeRechaza()
    {
        var envio = EnvioNoEntregado("RET-7");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            HandlerRetorno(envio, operadorId: Guid.NewGuid()).Handle(
                new RetornarADepositoCommand(envio.Id, Codigos(envio), "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Retorno_DeUnEnvioInexistente_DevuelveNoEncontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            HandlerRetorno(null).Handle(
                new RetornarADepositoCommand(Guid.NewGuid(), ["X-1"], "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Retorno_SinResponsable_SeRechaza()
    {
        var envio = EnvioNoEntregado("RET-8");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            HandlerRetorno(envio).Handle(
                new RetornarADepositoCommand(envio.Id, Codigos(envio), " "),
                CancellationToken.None));
    }

    [Fact]
    public async Task Retorno_SinEnvioNiBultos_SeRechaza()
    {
        var envio = EnvioNoEntregado("RET-9");
        var handler = HandlerRetorno(envio);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new RetornarADepositoCommand(Guid.Empty, Codigos(envio), "Operario"),
                CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new RetornarADepositoCommand(envio.Id, [], "Operario"),
                CancellationToken.None));
    }

    // ----- IniciarDevolucion -----

    [Fact]
    public async Task Devolucion_DeUnEnvioEnDeposito_PasaAEnDevolucionYLimpiaLaCache()
    {
        var envio = EnvioEnDepositoTrasIntento("DEV-1");

        await HandlerDevolucion(envio, reglas: _reglas).Handle(
            new IniciarDevolucionCommand(envio.Id, " Operario "),
            CancellationToken.None);

        Assert.Equal(EstadoEnvio.EnDevolucion, envio.Estado);
        Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.EnDevolucion, b.Estado));
        Assert.Equal(1, _unidad.Guardados);
        Assert.Equal(2, _cache.Eliminadas.Count);
        Assert.Contains($"envios:operador:{_operadorId}", _cache.Eliminadas);
        Assert.Equal("Operario", envio.Eventos.Last().Responsable);
    }

    [Fact]
    public async Task Devolucion_SinSesion_SeRechaza()
    {
        var envio = EnvioEnDepositoTrasIntento("DEV-2");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            HandlerDevolucion(envio, autenticado: false, reglas: _reglas).Handle(
                new IniciarDevolucionCommand(envio.Id, "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Devolucion_SinPermisos_SeRechaza()
    {
        var envio = EnvioEnDepositoTrasIntento("DEV-3");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            HandlerDevolucion(envio, Perfiles.Comercio, reglas: _reglas).Handle(
                new IniciarDevolucionCommand(envio.Id, "Operario"),
                CancellationToken.None));

        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Devolucion_DeUnEnvioDeOtroOperador_SeRechaza()
    {
        var envio = EnvioEnDepositoTrasIntento("DEV-4");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            HandlerDevolucion(envio, operadorId: Guid.NewGuid(), reglas: _reglas).Handle(
                new IniciarDevolucionCommand(envio.Id, "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Devolucion_DeUnEnvioInexistente_DevuelveNoEncontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            HandlerDevolucion(null, reglas: _reglas).Handle(
                new IniciarDevolucionCommand(Guid.NewGuid(), "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Devolucion_SiElEnvioNoEstaEnDeposito_SeRechaza()
    {
        var envio = NuevoEnvio("DEV-5");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandlerDevolucion(envio, reglas: _reglas).Handle(
                new IniciarDevolucionCommand(envio.Id, "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Devolucion_SinReglasOperativas_DevuelveNoEncontrado()
    {
        var envio = EnvioEnDepositoTrasIntento("DEV-6");

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            HandlerDevolucion(envio, reglas: null).Handle(
                new IniciarDevolucionCommand(envio.Id, "Operario"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Devolucion_SinResponsableNiEnvio_SeRechaza()
    {
        var envio = EnvioEnDepositoTrasIntento("DEV-7");
        var handler = HandlerDevolucion(envio, reglas: _reglas);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new IniciarDevolucionCommand(envio.Id, " "),
                CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new IniciarDevolucionCommand(Guid.Empty, "Operario"),
                CancellationToken.None));
    }

    // ----- Dobles de prueba -----

    private sealed class EnviosEnMemoria(Envio? envio) : IEnvioRepository
    {
        public Task AgregarAsync(Envio valor, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Envio?>(envio is not null && id == envio.Id ? envio : null);

        public Task<bool> ExisteReferenciaAsync(
            Guid cuentaId, string referencia, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Envio>> ListarAsync(
            Guid operadorId, Guid? cuentaId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(envio is null ? [] : [envio]);

        public Task<IReadOnlyList<Envio>> ListarPorCuentasComercialesAsync(
            IReadOnlyCollection<Guid> cuentasIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(envio is null ? [] : [envio]);

        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(
            Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(envio is null ? [] : [envio]);

        public Task<Envio?> ObtenerPorTokenSeguimientoAsync(
            string token, CancellationToken ct) =>
            Task.FromResult<Envio?>(null);

        public Task<Envio?> ObtenerPorComercioAsync(
            Guid envioId, Guid comercioId, CancellationToken ct) =>
            Task.FromResult<Envio?>(
                envio is not null && envio.Id == envioId ? envio : null);
    }

    private sealed class ReglasEnMemoria(VersionReglasOperativas? reglas)
        : IVersionReglasOperativasRepository
    {
        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
            Guid operadorId, Guid versionId, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(
                reglas is not null &&
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

    private sealed class TenantDePrueba(
        Guid usuarioId,
        Guid operadorId,
        string perfil,
        bool autenticado = true) : ITenantContext
    {
        public Guid? UsuarioId => usuarioId;
        public Guid? ComercioId => null;
        public Guid? OperadorId => operadorId;
        public string? Perfil => perfil;
        public bool EstaAutenticado => autenticado;
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class CacheRegistradora : ICacheService
    {
        public List<string> Eliminadas { get; } = [];

        public Task<T?> ObtenerAsync<T>(
            string clave, CancellationToken ct = default) =>
            Task.FromResult<T?>(default);

        public Task GuardarAsync<T>(
            string clave, T valor, TimeSpan duracion,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EliminarAsync(
            string clave, CancellationToken ct = default)
        {
            Eliminadas.Add(clave);
            return Task.CompletedTask;
        }
    }
}
