using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Application.CuentasComerciales.Gestion;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

/// <summary>RF 1: baja, reactivación y modificación de comercios por el administrador del operador.</summary>
public class GestionComerciosHandlersTests
{
    private static readonly Guid OperadorId = Guid.NewGuid();

    private readonly CuentasEnMemoria _cuentas = new();
    private readonly UnidadDeTrabajo _unidad = new();

    private static Sesion Admin(Guid? operadorId = null) =>
        new(operadorId ?? OperadorId, Perfiles.AdministradorOperador);

    private CuentaComercial CuentaActiva()
    {
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), OperadorId, Guid.NewGuid());
        _cuentas.Lista.Add(cuenta);
        return cuenta;
    }

    private CambiarActividadComercioHandler Actividad(Sesion sesion) =>
        new(_cuentas, _unidad, sesion);

    [Fact]
    public async Task DarDeBaja_desactiva_la_cuenta_y_guarda()
    {
        var cuenta = CuentaActiva();

        await Actividad(Admin()).Handle(
            new CambiarActividadComercioCommand(cuenta.Id, false), CancellationToken.None);

        Assert.False(cuenta.Activa);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Reactivar_vuelve_a_activar_una_cuenta_dada_de_baja()
    {
        var cuenta = CuentaActiva();
        cuenta.Desactivar();

        await Actividad(Admin()).Handle(
            new CambiarActividadComercioCommand(cuenta.Id, true), CancellationToken.None);

        Assert.True(cuenta.Activa);
    }

    [Fact]
    public async Task Reactivar_no_aplica_a_una_solicitud_pendiente()
    {
        var pendiente = CuentaComercial.Solicitar(Guid.NewGuid(), OperadorId, Guid.NewGuid());
        _cuentas.Lista.Add(pendiente);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Actividad(Admin()).Handle(
                new CambiarActividadComercioCommand(pendiente.Id, true), CancellationToken.None));

        Assert.False(pendiente.Activa);
    }

    [Fact]
    public async Task DarDeBaja_exige_perfil_administrador_del_operador()
    {
        var cuenta = CuentaActiva();
        var despachador = new Sesion(OperadorId, Perfiles.Operador);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Actividad(despachador).Handle(
                new CambiarActividadComercioCommand(cuenta.Id, false), CancellationToken.None));

        Assert.True(cuenta.Activa);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task DarDeBaja_no_encuentra_comercios_de_otro_operador()
    {
        var cuenta = CuentaActiva();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Actividad(Admin(Guid.NewGuid())).Handle(
                new CambiarActividadComercioCommand(cuenta.Id, false), CancellationToken.None));

        Assert.True(cuenta.Activa);
    }

    [Fact]
    public async Task ModificarDireccion_actualiza_la_direccion_de_recepcion()
    {
        var cuenta = CuentaActiva();
        var handler = new ModificarDireccionComercioHandler(_cuentas, _unidad, Admin());

        await handler.Handle(
            new ModificarDireccionComercioCommand(cuenta.Id, "Av. Italia", "1234", "Montevideo", "Montevideo"),
            CancellationToken.None);

        Assert.Equal("Av. Italia", cuenta.DireccionRecepcion!.Calle);
        Assert.Equal("1234", cuenta.DireccionRecepcion.Numero);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task ModificarDireccion_rechaza_datos_incompletos()
    {
        var cuenta = CuentaActiva();
        var handler = new ModificarDireccionComercioHandler(_cuentas, _unidad, Admin());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new ModificarDireccionComercioCommand(cuenta.Id, "", "1", "Montevideo", "Montevideo"),
                CancellationToken.None));

        Assert.Null(cuenta.DireccionRecepcion);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task ModificarDireccion_no_encuentra_comercios_de_otro_operador()
    {
        var cuenta = CuentaActiva();
        var handler = new ModificarDireccionComercioHandler(_cuentas, _unidad, Admin(Guid.NewGuid()));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(
                new ModificarDireccionComercioCommand(cuenta.Id, "Calle", "1", "Loc", "Dep"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Listar_exige_administrador_y_consulta_solo_su_operador()
    {
        var lectura = new LecturaEnMemoria();

        await new ListarComerciosDelOperadorHandler(lectura, Admin()).Handle(
            new ListarComerciosDelOperadorQuery(), CancellationToken.None);

        Assert.Equal(OperadorId, lectura.OperadorConsultado);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new ListarComerciosDelOperadorHandler(lectura, new Sesion(OperadorId, Perfiles.Operador))
                .Handle(new ListarComerciosDelOperadorQuery(), CancellationToken.None));
    }

    // ----- Dobles de prueba -----

    private sealed class Sesion(Guid? operadorId, string? perfil) : ITenantContext
    {
        public Guid? UsuarioId => Guid.NewGuid();
        public Guid? OperadorId => operadorId;
        public Guid? ComercioId => null;
        public string? Perfil => perfil;
        public bool EstaAutenticado => true;
    }

    private sealed class LecturaEnMemoria : IComerciosDelOperadorLectura
    {
        public Guid? OperadorConsultado { get; private set; }

        public Task<IReadOnlyList<ComercioDelOperadorDto>> ListarAsync(Guid operadorId, CancellationToken ct)
        {
            OperadorConsultado = operadorId;
            return Task.FromResult<IReadOnlyList<ComercioDelOperadorDto>>([]);
        }
    }

    private sealed class CuentasEnMemoria : ICuentaComercialRepository
    {
        public List<CuentaComercial> Lista { get; } = [];

        public Task<CuentaComercial?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(c => c.Id == id));

        public Task<CuentaComercial?> ObtenerPorComercioAsync(
            Guid cuentaComercialId, Guid comercioId, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(c => c.Id == cuentaComercialId && c.ComercioId == comercioId));

        public Task<CuentaComercial?> ObtenerPorOperadorYComercioAsync(
            Guid operadorId, Guid comercioId, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(c => c.OperadorId == operadorId && c.ComercioId == comercioId));

        public Task AgregarAsync(CuentaComercial cuenta, CancellationToken ct)
        {
            Lista.Add(cuenta);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(
            Guid operadorId, Guid? cuentaComercialId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CuentaComercialDto>>([]);

        public Task<IReadOnlyList<SolicitudPendienteDto>> ListarSolicitudesPendientesAsync(
            Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SolicitudPendienteDto>>([]);

        public Task<IReadOnlyList<CuentaComercialDto>> ListarPorComercioAsync(
            Guid comercioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CuentaComercialDto>>([]);
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> operacion, CancellationToken ct) =>
            operacion(ct);
    }
}
