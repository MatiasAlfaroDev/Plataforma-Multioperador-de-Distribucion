
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Application.Retiros.Commands.GestionarRetiro;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;
using Xunit;

namespace UltimaMilla.UnitTests;

public sealed class GestionarRetiroHandlerTests
{
    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Retiro _retiro;
    private readonly HojaDeRuta _hoja;
    private readonly UnidadDeTrabajo _unidad = new();

    private readonly GestionarRetiroHandler _handler;

    public GestionarRetiroHandlerTests()
    {
        var ahora = DateTimeOffset.UtcNow;
        var fecha = DateOnly.FromDateTime(
            ahora.ToOffset(TimeSpan.FromHours(-3)).DateTime);

        _retiro = Retiro.Crear(
            _operadorId,
            Guid.NewGuid(),
            "Devolución de mercadería",
            new Direccion("Origen", "100", "San José", "San José"),
            new Direccion("Comercio", "200", "San José", "San José"),
            fecha,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [BultoRetiro.Crear("RET-001", 5m, 20, 20, 20)],
            ahora);

        _retiro.AsignarARuta(Guid.NewGuid());
        _retiro.RegistrarRetiro(ahora, "foto.jpg", "firma.png");
        _retiro.RecibirEnDeposito();

        var repartidor = Repartidor.Crear(
            _operadorId, "Repartidor", "099123456");

        var vehiculo = Vehiculo.Crear(
            _operadorId, "STP1234", 100m, 10m);

        _hoja = HojaDeRuta.Crear(
            _operadorId, repartidor, vehiculo, fecha, 10);

        _hoja.AgregarEntregaRetiroComercio(_retiro);

        var repositorioRetiros = new RetirosEnMemoria(_retiro);

        var avisos = new RegistrarAvisosRetiroHandler(
            repositorioRetiros,
            new SuscripcionesVacias(),
            new EntregasVacias(),
            new PublicadorFalso());

        _handler = new GestionarRetiroHandler(
            repositorioRetiros,
            new HojasEnMemoria(_hoja),
            _unidad,
            new TenantDePrueba(_operadorId),
            avisos,
            new RelojFijo(ahora));
    }

    [Fact]
    public async Task EntregaConfirmada_LiberaParadaYGuarda()
    {
        _hoja.MarcarDespachada(DateTimeOffset.UtcNow);

        await _handler.EntregarAlComercioAsync(
            _retiro.Id,
            CancellationToken.None);

        Assert.Equal(
            EstadoRetiro.EntregadoAlComercio,
            _retiro.Estado);

        Assert.False(Assert.Single(_hoja.Paradas).Activa);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task HojaSinDespachar_RechazaEntrega()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.EntregarAlComercioAsync(
                _retiro.Id,
                CancellationToken.None));

        Assert.Equal(EstadoRetiro.EnDeposito, _retiro.Estado);
        Assert.True(Assert.Single(_hoja.Paradas).Activa);
        Assert.Equal(0, _unidad.Guardados);
    }

    private sealed class RetirosEnMemoria(Retiro retiro)
        : IRetiroRepository
    {
        public Task<Retiro?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<Retiro?>(
                id == retiro.Id ? retiro : null);

        public Task AgregarAsync(
            Retiro valor, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Retiro>> ListarAsync(
            Guid operadorId,
            Guid? cuentaComercialId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>([retiro]);
    
        public Task<IReadOnlyList<Retiro>> ListarParaPlanificacionAsync(
            Guid operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>(
                retiro.OperadorId == operadorId &&
                retiro.Estado == EstadoRetiro.Solicitado &&
                retiro.HojaDeRutaId == null
                    ? [retiro]
                    : []);

        public Task<IReadOnlyList<Retiro>> ListarParaEntregaComercioAsync(
            Guid operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>(
                retiro.OperadorId == operadorId &&
                retiro.Estado == EstadoRetiro.EnDeposito
                    ? [retiro]
                    : []);
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
                hoja.OperadorId == operadorId && hoja.Id == hojaId
                    ? hoja : null);

        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
            Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>([hoja]);

        public Task<IReadOnlyList<HojaDeRuta>> ListarDelRepartidorAsync(
            Guid operadorId,
            Guid repartidorId,
            DateOnly fecha,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>([hoja]);

        public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
            Guid operadorId, Guid envioId, CancellationToken ct) =>
            Task.FromResult<HojaDeRuta?>(null);

        public Task<HojaDeRuta?>
            ObtenerPorRetiroEnEntregaActivoAsync(
                Guid operadorId,
                Guid retiroId,
                CancellationToken ct) =>
            Task.FromResult<HojaDeRuta?>(
                hoja.OperadorId == operadorId &&
                hoja.Paradas.Any(p =>
                    p.Activa &&
                    p.RetiroId == retiroId &&
                    p.TipoOperacion ==
                        TipoOperacionRuta.EntregaRetiroComercio)
                    ? hoja : null);
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

    private sealed class TenantDePrueba(Guid operadorId)
        : ITenantContext
    {
        public Guid? OperadorId => operadorId;
        public Guid? UsuarioId => Guid.Empty;
        public Guid? ComercioId => null;
        public string? Perfil => Perfiles.Operador;
        public bool EstaAutenticado => true;
    }

    private sealed class RelojFijo(DateTimeOffset ahora)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class SuscripcionesVacias
        : ISuscripcionAvisoRepository
    {
        public Task<SuscripcionAviso?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<SuscripcionAviso?>(null);

        public Task<IReadOnlyList<SuscripcionAviso>> ListarAsync(
            IReadOnlyCollection<Guid>? cuentas, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuscripcionAviso>>([]);

        public Task AgregarAsync(
            SuscripcionAviso valor, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class EntregasVacias
        : IEntregaAvisoRepository
    {
        public Task<EntregaAviso?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<EntregaAviso?>(null);

        public Task<EntregaAviso?> ObtenerAsync(
            Guid id, IReadOnlyCollection<Guid>? cuentas, CancellationToken ct) =>
            Task.FromResult<EntregaAviso?>(null);

        public Task<bool> ExisteAsync(
            Guid suscripcionId, string clave, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<EntregaAviso>> ListarAsync(
            IReadOnlyCollection<Guid>? cuentas,
            EstadoEntregaAviso? estado,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntregaAviso>>([]);

        public Task<IReadOnlyList<EntregaAviso>> ListarFallidasAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntregaAviso>>([]);

        public Task<IReadOnlyList<Guid>> OperadoresConFallidasAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task AgregarAsync(
            EntregaAviso valor, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class PublicadorFalso : IPublicadorDeEventos
    {
        public Task PublicarAsync<T>(
            T evento, CancellationToken ct)
            where T : class => Task.CompletedTask;
    }
}
