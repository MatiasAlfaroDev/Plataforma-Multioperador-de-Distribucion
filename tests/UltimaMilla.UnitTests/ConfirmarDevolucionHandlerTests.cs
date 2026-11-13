
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;
using UltimaMilla.Application.Envios.Commands.ConfirmarDevolucion;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;
using Xunit;

namespace UltimaMilla.UnitTests;

public sealed class ConfirmarDevolucionHandlerTests
{
    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _comercioId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();

    private readonly CuentaComercial _cuenta;
    private readonly Envio _envio;
    private readonly HojaDeRuta _hoja;
    private readonly VersionReglasOperativas _reglas;

    private readonly UnidadDeTrabajo _unidad = new();
    private readonly ConfirmarDevolucionHandler _handler;

    public ConfirmarDevolucionHandlerTests()
    {
        var ahora = DateTimeOffset.UtcNow;

        _cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            _operadorId,
            _comercioId);

        _reglas = VersionReglasOperativas.Crear(
            _operadorId,
            maximoIntentosEntrega: 1,
            horasMinimasEntreIntentos: 24,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 7,
            vigenciaDesde: ahora.AddDays(-1));

        _envio = Envio.Crear(
            _cuenta,
            $"DEV-{Guid.NewGuid():N}",
            Modalidad.Estandar,
            new Destinatario("Destinatario", "099123456"),
            new Direccion("Origen", "100", "San José", "San José"),
            [new NuevoBulto(5m, 20, 20, 20)],
            ahora);

        _envio.AsignarReglasOperativas(_reglas.Id);

        // Recorrido real hasta EnDevolucion.
        Aplicar(DisparadorEnvio.RecibirEnDeposito);
        Aplicar(DisparadorEnvio.AsignarARuta);
        Aplicar(DisparadorEnvio.Despachar);

        var motivo = MotivoNoEntrega.Crear(
            _operadorId,
            _reglas.Id,
            "AUSENTE",
            "Destinatario ausente",
            requiereDevolucion: true);

        _envio.Aplicar(
            DisparadorEnvio.RegistrarIntentoFallido,
            _reglas,
            ahora,
            "Prueba",
            "Repartidor",
            motivoNoEntrega: motivo,
            evidenciaFallo: "foto-fallo");

        Aplicar(DisparadorEnvio.RetornarADeposito);
        Aplicar(DisparadorEnvio.IniciarDevolucion);

        var repartidor = Repartidor.Crear(
            _operadorId,
            "Repartidor",
            "099123456");

        var vehiculo = Vehiculo.Crear(
            _operadorId,
            "STP1234",
            100m,
            10m);

        var fecha = DateOnly.FromDateTime(
            ahora.ToOffset(TimeSpan.FromHours(-3)).DateTime);

        _hoja = HojaDeRuta.Crear(
            _operadorId,
            repartidor,
            vehiculo,
            fecha,
            10);

        _hoja.AgregarDevolucionComercio(_envio);

        _handler = new ConfirmarDevolucionHandler(
            new EnviosEnMemoria(_envio),
            new CuentasEnMemoria(_cuenta),
            new ReglasEnMemoria(_reglas),
            _unidad,
            new TenantDePrueba(_usuarioId, _comercioId),
            new OperadorActivoFalso(),
            new RelojFijo(ahora),
            new CacheFalsa(),
            new HojasEnMemoria(_hoja));
    }

    private void Aplicar(DisparadorEnvio disparador)
    {
        _envio.Aplicar(
            disparador,
            _reglas,
            DateTimeOffset.UtcNow,
            "Prueba",
            "Operario");
    }

    private ConfirmarDevolucionCommand CrearComando() =>
        new(
            _cuenta.Id,
            _envio.Id,
            _envio.Bultos
                .Where(b => b.Estado == EstadoBulto.EnDevolucion)
                .Select(b => b.Identificacion)
                .ToList(),
            "Responsable",
            nameof(ModalidadDevolucion.EntregaDirecta),
            "Encargado del comercio",
            "CONST-001");

    [Fact]
    public async Task DevolucionConfirmada_LiberaParadaYGuarda()
    {
        _hoja.MarcarDespachada(DateTimeOffset.UtcNow);

        await _handler.Handle(
            CrearComando(),
            CancellationToken.None);

        Assert.Equal(EstadoEnvio.Devuelto, _envio.Estado);

        Assert.All(
            _envio.Bultos,
            b => Assert.Equal(EstadoBulto.Devuelto, b.Estado));

        Assert.False(Assert.Single(_hoja.Paradas).Activa);

        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task HojaSinDespachar_RechazaDevolucion()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(
                CrearComando(),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnDevolucion, _envio.Estado);
        Assert.True(Assert.Single(_hoja.Paradas).Activa);
        Assert.Equal(0, _unidad.Guardados);
    }

    private sealed class EnviosEnMemoria(Envio envio)
        : IEnvioRepository
    {
        public Task AgregarAsync(Envio valor, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<Envio?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<Envio?>(
                id == envio.Id ? envio : null);

        public Task<bool> ExisteReferenciaAsync(
            Guid cuentaId, string referencia, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Envio>> ListarAsync(
            Guid operadorId, Guid? cuentaId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task<IReadOnlyList<Envio>> ListarPorCuentasComercialesAsync(
            IReadOnlyCollection<Guid> cuentasIds,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(
            Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task<Envio?> ObtenerPorTokenSeguimientoAsync(
            string token, CancellationToken ct) =>
            Task.FromResult<Envio?>(null);

        public Task<Envio?> ObtenerPorComercioAsync(
            Guid envioId, Guid comercioId, CancellationToken ct) =>
            Task.FromResult<Envio?>(
                envio.Id == envioId ? envio : null);
    }

    private sealed class CuentasEnMemoria(CuentaComercial cuenta)
        : ICuentaComercialRepository
    {
        public Task<CuentaComercial?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<CuentaComercial?>(
                cuenta.Id == id ? cuenta : null);

        public Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(
            Guid operadorId, Guid? cuentaId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CuentaComercialDto>>([]);

        public Task<CuentaComercial?> ObtenerPorOperadorYComercioAsync(
            Guid operadorId, Guid comercioId, CancellationToken ct) =>
            Task.FromResult<CuentaComercial?>(
                cuenta.OperadorId == operadorId &&
                cuenta.ComercioId == comercioId ? cuenta : null);

        public Task AgregarAsync(
            CuentaComercial valor, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SolicitudPendienteDto>>
            ListarSolicitudesPendientesAsync(
                Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SolicitudPendienteDto>>([]);

        public Task<IReadOnlyList<CuentaComercialDto>>
            ListarPorComercioAsync(
                Guid comercioId, CancellationToken ct)
        {
            IReadOnlyList<CuentaComercialDto> resultado =
                cuenta.ComercioId == comercioId
                    ? [
                        new CuentaComercialDto(
                            cuenta.Id,
                            cuenta.OperadorId,
                            "Operador de prueba",
                            cuenta.ComercioId,
                            "Comercio de prueba",
                            cuenta.Activa,
                            cuenta.Estado)
                    ]
                    : [];

            return Task.FromResult(resultado);
        }
    }

    private sealed class ReglasEnMemoria(
        VersionReglasOperativas reglas)
        : IVersionReglasOperativasRepository
    {
        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
            Guid operadorId, Guid versionId, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(
                reglas.OperadorId == operadorId &&
                reglas.Id == versionId ? reglas : null);

        public Task<VersionReglasOperativas?> ObtenerVigenteAsync(
            Guid operadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(reglas);

        public Task<VersionReglasOperativas?>
            ObtenerVigenteParaActualizarAsync(
                Guid operadorId,
                DateTimeOffset fecha,
                CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(reglas);

        public Task AgregarAsync(
            VersionReglasOperativas version,
            CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class HojasEnMemoria(HojaDeRuta hoja)
        : IHojaDeRutaRepository
    {
        public Task AgregarAsync(
            HojaDeRuta valor, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<HojaDeRuta?> ObtenerAsync(
            Guid operadorId, Guid hojaId, CancellationToken ct) =>
            Task.FromResult<HojaDeRuta?>(
                hoja.OperadorId == operadorId &&
                hoja.Id == hojaId ? hoja : null);

        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
            Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>([hoja]);

        public Task<IReadOnlyList<HojaDeRuta>>
            ListarDelRepartidorAsync(
                Guid operadorId,
                Guid repartidorId,
                DateOnly fecha,
                CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>([hoja]);

        public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
            Guid operadorId, Guid envioId, CancellationToken ct) =>
            Task.FromResult<HojaDeRuta?>(
                hoja.OperadorId == operadorId &&
                hoja.Paradas.Any(p =>
                    p.Activa && p.EnvioId == envioId)
                    ? hoja : null);

        public Task<HojaDeRuta?>
            ObtenerPorRetiroEnEntregaActivoAsync(
                Guid operadorId,
                Guid retiroId,
                CancellationToken ct) =>
            Task.FromResult<HojaDeRuta?>(null);
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
            Func<CancellationToken, Task> operacion,
            CancellationToken ct) => operacion(ct);
    }

    private sealed class TenantDePrueba(
        Guid usuarioId,
        Guid comercioId) : ITenantContext
    {
        public Guid? UsuarioId => usuarioId;
        public Guid? ComercioId => comercioId;
        public Guid? OperadorId => null;
        public string? Perfil => Perfiles.Comercio;
        public bool EstaAutenticado => true;
    }

    private sealed class OperadorActivoFalso : IOperadorActivo
    {
        public void SeleccionarOperador(Guid operadorId) { }
    }

    private sealed class RelojFijo(DateTimeOffset ahora)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class CacheFalsa : ICacheService
    {
        public Task<T?> ObtenerAsync<T>(
            string clave,
            CancellationToken ct = default) =>
            Task.FromResult<T?>(default);

        public Task GuardarAsync<T>(
            string clave,
            T valor,
            TimeSpan duracion,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EliminarAsync(
            string clave,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task EliminarPorPrefijoAsync(
            string prefijo,
            CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
