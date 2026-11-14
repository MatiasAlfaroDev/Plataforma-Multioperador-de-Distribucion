using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;
using UltimaMilla.Application.Retiros.Commands.CancelarRetiro;
using UltimaMilla.Application.Rutas.Commands.QuitarRetiroDeRuta;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;
using Xunit;

namespace UltimaMilla.UnitTests;

public sealed class CancelarYQuitarRetiroHandlersTests
{
    private static readonly Guid OperadorId = Guid.NewGuid();
    private static readonly Guid ComercioId = Guid.NewGuid();
    private static readonly Guid CuentaId = Guid.NewGuid();
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);

    private readonly Retiro _retiro = Retiro.Crear(
        OperadorId,
        CuentaId,
        "Cambio de talle",
        new Direccion("Origen", "100", "San José", "San José"),
        new Direccion("Comercio", "200", "San José", "San José"),
        Hoy,
        new TimeOnly(9, 0),
        new TimeOnly(17, 0),
        [BultoRetiro.Crear("RET-001", 5m, 20, 20, 20)],
        DateTimeOffset.UtcNow);

    private readonly UnidadDeTrabajo _unidad = new();

    private CancelarRetiroHandler Cancelar(ITenantContext sesion, OperadorActivoEspia? activo = null)
    {
        var retiros = new RetirosEnMemoria([_retiro]);
        var avisos = new RegistrarAvisosRetiroHandler(
            retiros, new SuscripcionesVacias(), new EntregasVacias(), new PublicadorFalso());

        return new CancelarRetiroHandler(
            retiros,
            new CuentasDelComercio(),
            _unidad,
            sesion,
            activo ?? new OperadorActivoEspia(),
            avisos,
            TimeProvider.System);
    }

    // ----- CancelarRetiro -----

    [Fact]
    public async Task El_despacho_cancela_un_retiro_de_su_operador()
    {
        await Cancelar(Sesion.Despacho(OperadorId))
            .Handle(new CancelarRetiroCommand(_retiro.Id, "Lo pidió el comercio"), CancellationToken.None);

        Assert.Equal(EstadoRetiro.Cancelado, _retiro.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task El_comercio_cancela_su_retiro_fijando_el_operador_de_la_cuenta()
    {
        var activo = new OperadorActivoEspia();

        await Cancelar(Sesion.Comercio(ComercioId), activo)
            .Handle(new CancelarRetiroCommand(_retiro.Id, "El comprador se arrepintió"), CancellationToken.None);

        Assert.Equal(EstadoRetiro.Cancelado, _retiro.Estado);
        Assert.Equal(OperadorId, activo.Seleccionado);
    }

    [Fact]
    public async Task Otro_operador_u_otro_comercio_no_encuentran_el_retiro()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            Cancelar(Sesion.Despacho(Guid.NewGuid()))
                .Handle(new CancelarRetiroCommand(_retiro.Id, "Motivo"), CancellationToken.None));

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            Cancelar(Sesion.Comercio(Guid.NewGuid()))
                .Handle(new CancelarRetiroCommand(_retiro.Id, "Motivo"), CancellationToken.None));

        Assert.Equal(EstadoRetiro.Solicitado, _retiro.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task El_operario_de_deposito_no_puede_cancelar()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Cancelar(new Sesion(OperadorId, null, Perfiles.OperarioDeposito))
                .Handle(new CancelarRetiroCommand(_retiro.Id, "Motivo"), CancellationToken.None));
    }

    [Fact]
    public async Task Cancelar_valida_el_retiro_y_el_motivo()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Cancelar(Sesion.Despacho(OperadorId))
                .Handle(new CancelarRetiroCommand(Guid.Empty, "Motivo"), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Cancelar(Sesion.Despacho(OperadorId))
                .Handle(new CancelarRetiroCommand(_retiro.Id, " "), CancellationToken.None));

        Assert.Equal(0, _unidad.Guardados);
    }

    // ----- QuitarRetiroDeRuta -----

    private (HojaDeRuta hoja, QuitarRetiroDeRutaHandler handler) HojaConElRetiro()
    {
        var hoja = HojaDeRuta.Crear(
            OperadorId,
            Repartidor.Crear(OperadorId, "Repartidor", "099123456"),
            Vehiculo.Crear(OperadorId, "STP1234", 100m, 10m),
            Hoy,
            5);
        hoja.AgregarRetiro(_retiro);
        _retiro.AsignarARuta(hoja.Id);

        return (hoja, new QuitarRetiroDeRutaHandler(
            new HojaUnica(hoja), new RetirosEnMemoria([_retiro]), _unidad, TimeProvider.System));
    }

    [Fact]
    public async Task Quitar_libera_la_parada_y_el_retiro_vuelve_a_solicitado()
    {
        var (hoja, handler) = HojaConElRetiro();

        await handler.Handle(new QuitarRetiroDeRutaCommand(OperadorId, hoja.Id, _retiro.Id), CancellationToken.None);

        Assert.Equal(EstadoRetiro.Solicitado, _retiro.Estado);
        Assert.False(Assert.Single(hoja.Paradas).Activa);
        Assert.Equal(2, _unidad.Guardados);
    }

    [Fact]
    public async Task Quitar_valida_hoja_y_retiro()
    {
        var (hoja, handler) = HojaConElRetiro();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new QuitarRetiroDeRutaCommand(OperadorId, Guid.Empty, _retiro.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            handler.Handle(new QuitarRetiroDeRutaCommand(OperadorId, Guid.NewGuid(), _retiro.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            handler.Handle(new QuitarRetiroDeRutaCommand(OperadorId, hoja.Id, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(EstadoRetiro.AsignadoARuta, _retiro.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    // ----- Dobles de prueba -----

    private sealed record Sesion(Guid? OperadorId, Guid? ComercioId, string? Perfil) : ITenantContext
    {
        public static Sesion Despacho(Guid operadorId) => new(operadorId, null, Perfiles.Despachador);
        public static Sesion Comercio(Guid comercioId) => new(null, comercioId, Perfiles.AdministradorComercio);

        public Guid? UsuarioId => Guid.NewGuid();
        public bool EstaAutenticado => true;
    }

    private sealed class OperadorActivoEspia : IOperadorActivo
    {
        public Guid? Seleccionado { get; private set; }
        public void SeleccionarOperador(Guid operadorId) => Seleccionado = operadorId;
    }

    /// <summary>El comercio de prueba tiene una sola cuenta confirmada, con el operador de prueba.</summary>
    private sealed class CuentasDelComercio : ICuentaComercialRepository
    {
        public Task<IReadOnlyList<CuentaComercialDto>> ListarPorComercioAsync(Guid comercioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CuentaComercialDto>>(comercioId == ComercioId
                ? [new CuentaComercialDto(CuentaId, OperadorId, "Operador", ComercioId, "Comercio", true, EstadoCuentaComercial.Confirmada)]
                : []);

        public Task<CuentaComercial?> ObtenerAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(Guid operadorId, Guid? cuentaComercialId, CancellationToken ct) => throw new NotSupportedException();
        public Task<CuentaComercial?> ObtenerPorOperadorYComercioAsync(Guid operadorId, Guid comercioId, CancellationToken ct) => throw new NotSupportedException();
        public Task AgregarAsync(CuentaComercial cuenta, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SolicitudPendienteDto>> ListarSolicitudesPendientesAsync(Guid operadorId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class HojaUnica(HojaDeRuta hoja) : IHojaDeRutaRepository
    {
        public Task<HojaDeRuta?> ObtenerAsync(Guid operadorId, Guid hojaId, CancellationToken ct) =>
            Task.FromResult<HojaDeRuta?>(hoja.OperadorId == operadorId && hoja.Id == hojaId ? hoja : null);

        public Task AgregarAsync(HojaDeRuta valor, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(Guid operadorId, CancellationToken ct) => Task.FromResult<IReadOnlyList<HojaDeRuta>>([hoja]);
        public Task<IReadOnlyList<HojaDeRuta>> ListarDelRepartidorAsync(Guid operadorId, Guid repartidorId, DateOnly fecha, CancellationToken ct) => Task.FromResult<IReadOnlyList<HojaDeRuta>>([hoja]);
        public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(Guid operadorId, Guid envioId, CancellationToken ct) => Task.FromResult<HojaDeRuta?>(null);
        public Task<HojaDeRuta?> ObtenerPorRetiroEnEntregaActivoAsync(Guid operadorId, Guid retiroId, CancellationToken ct) => Task.FromResult<HojaDeRuta?>(null);
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> operacion, CancellationToken ct) => operacion(ct);
    }

    private sealed class SuscripcionesVacias : ISuscripcionAvisoRepository
    {
        public Task<SuscripcionAviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult<SuscripcionAviso?>(null);
        public Task<IReadOnlyList<SuscripcionAviso>> ListarAsync(IReadOnlyCollection<Guid>? cuentas, CancellationToken ct) => Task.FromResult<IReadOnlyList<SuscripcionAviso>>([]);
        public Task AgregarAsync(SuscripcionAviso valor, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class EntregasVacias : IEntregaAvisoRepository
    {
        public Task<EntregaAviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult<EntregaAviso?>(null);
        public Task<EntregaAviso?> ObtenerAsync(Guid id, IReadOnlyCollection<Guid>? cuentas, CancellationToken ct) => Task.FromResult<EntregaAviso?>(null);
        public Task<bool> ExisteAsync(Guid suscripcionId, string clave, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<EntregaAviso>> ListarAsync(IReadOnlyCollection<Guid>? cuentas, EstadoEntregaAviso? estado, CancellationToken ct) => Task.FromResult<IReadOnlyList<EntregaAviso>>([]);
        public Task<IReadOnlyList<EntregaAviso>> ListarFallidasAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<EntregaAviso>>([]);
        public Task<IReadOnlyList<Guid>> OperadoresConFallidasAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task AgregarAsync(EntregaAviso valor, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class PublicadorFalso : IPublicadorDeEventos
    {
        public Task PublicarAsync<T>(T evento, CancellationToken ct) where T : class => Task.CompletedTask;
    }
}
