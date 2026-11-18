using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Application.CuentasComerciales.Commands.AceptarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Commands.CambiarEstadoCuentaComercial;
using UltimaMilla.Application.CuentasComerciales.Commands.ModificarComercio;
using UltimaMilla.Application.CuentasComerciales.Commands.ConfigurarDireccionRecepcion;
using UltimaMilla.Application.CuentasComerciales.Commands.RechazarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Commands.SolicitarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;
using UltimaMilla.Application.Seguridad;
using FluentValidation;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

public class CuentasComercialesHandlersTests
{
    private static readonly Guid OperadorId = Guid.NewGuid();
    private static readonly Guid ComercioId = Guid.NewGuid();

    private readonly CuentasEnMemoria _cuentas = new();
    private readonly OperadoresEnMemoria _operadores = new();
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();

    private static SesionFalsa AdminOperador(Guid? operadorId = null) =>
        new(operadorId ?? OperadorId, null, Perfiles.AdministradorOperador, autenticado: true);

    private static SesionFalsa AdminComercio(Guid? comercioId = null) =>
        new(null, comercioId ?? ComercioId, Perfiles.AdministradorComercio, autenticado: true);

    private CuentaComercial Pendiente()
    {
        var cuenta = CuentaComercial.Solicitar(Guid.NewGuid(), OperadorId, ComercioId);
        _cuentas.Lista.Add(cuenta);
        return cuenta;
    }

    // ----- AceptarVinculacion -----

    [Fact]
    public async Task Aceptar_confirma_la_solicitud_y_guarda()
    {
        var solicitud = Pendiente();
        var handler = new AceptarVinculacionHandler(_cuentas, _unidad, AdminOperador());

        await handler.Handle(new AceptarVinculacionCommand(solicitud.Id), CancellationToken.None);

        Assert.Equal(EstadoCuentaComercial.Confirmada, solicitud.Estado);
        Assert.True(solicitud.Activa);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Aceptar_exige_sesion_de_operador()
    {
        var solicitud = Pendiente();
        var sinSesion = new SesionFalsa(null, null, null, autenticado: false);
        var handler = new AceptarVinculacionHandler(_cuentas, _unidad, sinSesion);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new AceptarVinculacionCommand(solicitud.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Aceptar_exige_perfil_administrador_del_operador()
    {
        var solicitud = Pendiente();
        var despachador = new SesionFalsa(OperadorId, null, Perfiles.Operador, autenticado: true);
        var handler = new AceptarVinculacionHandler(_cuentas, _unidad, despachador);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new AceptarVinculacionCommand(solicitud.Id), CancellationToken.None));
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Aceptar_no_encuentra_solicitudes_de_otro_operador()
    {
        var solicitud = Pendiente();
        var otroOperador = new AceptarVinculacionHandler(_cuentas, _unidad, AdminOperador(Guid.NewGuid()));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            otroOperador.Handle(new AceptarVinculacionCommand(solicitud.Id), CancellationToken.None));
        Assert.Equal(EstadoCuentaComercial.Pendiente, solicitud.Estado);
    }

    [Fact]
    public async Task Aceptar_una_solicitud_inexistente_da_no_encontrado()
    {
        var handler = new AceptarVinculacionHandler(_cuentas, _unidad, AdminOperador());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new AceptarVinculacionCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // ----- RechazarVinculacion -----

    [Fact]
    public async Task Rechazar_deja_la_solicitud_rechazada_y_guarda()
    {
        var solicitud = Pendiente();
        var handler = new RechazarVinculacionHandler(_cuentas, _unidad, AdminOperador());

        await handler.Handle(new RechazarVinculacionCommand(solicitud.Id), CancellationToken.None);

        Assert.Equal(EstadoCuentaComercial.Rechazada, solicitud.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Rechazar_exige_sesion_y_perfil_de_administrador()
    {
        var solicitud = Pendiente();
        var sinSesion = new RechazarVinculacionHandler(
            _cuentas, _unidad, new SesionFalsa(null, null, null, autenticado: false));
        var sinPerfil = new RechazarVinculacionHandler(
            _cuentas, _unidad, new SesionFalsa(OperadorId, null, Perfiles.Operador, autenticado: true));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sinSesion.Handle(new RechazarVinculacionCommand(solicitud.Id), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sinPerfil.Handle(new RechazarVinculacionCommand(solicitud.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Rechazar_no_encuentra_solicitudes_de_otro_operador()
    {
        var solicitud = Pendiente();
        var otroOperador = new RechazarVinculacionHandler(_cuentas, _unidad, AdminOperador(Guid.NewGuid()));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            otroOperador.Handle(new RechazarVinculacionCommand(solicitud.Id), CancellationToken.None));
    }

    // ----- SolicitarVinculacion -----

    private SolicitarVinculacionHandler Solicitar(ITenantContext sesion) =>
        new(_cuentas, _operadores, _unidad, sesion);

    private static SolicitarVinculacionCommand Pedido(Guid operadorId, bool conDireccion = true) =>
        new(operadorId, conDireccion
            ? new DireccionRecepcionCommand("18 de Julio", "1234", "San José de Mayo", "San José")
            : null!);

    [Fact]
    public async Task Solicitar_crea_una_cuenta_pendiente_con_la_direccion_de_recepcion()
    {
        _operadores.Lista.Add(Operador.Crear(OperadorId, "Operador Uno", "#112233", "a@b.test"));

        var id = await Solicitar(AdminComercio()).Handle(Pedido(OperadorId), CancellationToken.None);

        var cuenta = Assert.Single(_cuentas.Lista);
        Assert.Equal(id, cuenta.Id);
        Assert.Equal(OperadorId, cuenta.OperadorId);
        Assert.Equal(ComercioId, cuenta.ComercioId);
        Assert.Equal(EstadoCuentaComercial.Pendiente, cuenta.Estado);
        Assert.NotNull(cuenta.DireccionRecepcion);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Solicitar_exige_sesion_de_comercio_y_perfil_de_administrador()
    {
        var sinSesion = new SesionFalsa(null, null, null, autenticado: false);
        var sinPerfil = new SesionFalsa(null, ComercioId, Perfiles.Comercio, autenticado: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Solicitar(sinSesion).Handle(Pedido(OperadorId), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Solicitar(sinPerfil).Handle(Pedido(OperadorId), CancellationToken.None));
    }

    [Fact]
    public async Task Solicitar_rechaza_un_operador_vacio_o_inexistente()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Solicitar(AdminComercio()).Handle(Pedido(Guid.Empty), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Solicitar(AdminComercio()).Handle(Pedido(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Solicitar_rechaza_una_vinculacion_duplicada()
    {
        _operadores.Lista.Add(Operador.Crear(OperadorId, "Operador Uno", "#112233", "a@b.test"));
        Pendiente();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Solicitar(AdminComercio()).Handle(Pedido(OperadorId), CancellationToken.None));
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Solicitar_exige_la_direccion_de_recepcion()
    {
        _operadores.Lista.Add(Operador.Crear(OperadorId, "Operador Uno", "#112233", "a@b.test"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Solicitar(AdminComercio()).Handle(Pedido(OperadorId, conDireccion: false), CancellationToken.None));
        Assert.Empty(_cuentas.Lista);
    }

    // ----- ConfigurarDireccionRecepcion -----

    private ConfigurarDireccionRecepcionHandler Configurar(ITenantContext sesion) =>
        new(_cuentas, _unidad, sesion, new OperadorActivoFalso());

    private static ConfigurarDireccionRecepcionCommand NuevaDireccion(Guid cuentaId) =>
        new(cuentaId, "Rivera", "55", "Libertad", "San José");

    [Fact]
    public async Task Configurar_direccion_actualiza_la_cuenta_del_propio_comercio()
    {
        var cuenta = Pendiente();

        await Configurar(AdminComercio()).Handle(NuevaDireccion(cuenta.Id), CancellationToken.None);

        Assert.NotNull(cuenta.DireccionRecepcion);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Configurar_direccion_exige_administrador_del_comercio()
    {
        var cuenta = Pendiente();
        var sinPerfil = new SesionFalsa(null, ComercioId, Perfiles.Comercio, autenticado: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Configurar(sinPerfil).Handle(NuevaDireccion(cuenta.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Configurar_direccion_valida_el_comando_y_la_pertenencia()
    {
        var cuenta = Pendiente();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Configurar(AdminComercio()).Handle(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Configurar(AdminComercio()).Handle(NuevaDireccion(Guid.Empty), CancellationToken.None));
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            Configurar(AdminComercio(Guid.NewGuid())).Handle(NuevaDireccion(cuenta.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            Configurar(AdminComercio()).Handle(NuevaDireccion(Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, _unidad.Guardados);
    }

    // ----- Baja y reactivación (RF 1) -----

    private CuentaComercial Aceptada()
    {
        var cuenta = Pendiente();
        cuenta.Confirmar();
        return cuenta;
    }

    private CambiarEstadoCuentaComercialHandler CambiarEstado(ITenantContext sesion) =>
        new(_cuentas, _unidad, sesion);

    [Fact]
    public async Task Dar_de_baja_desactiva_la_cuenta_sin_cambiar_su_estado()
    {
        var cuenta = Aceptada();

        await CambiarEstado(AdminOperador()).Handle(
            new CambiarEstadoCuentaComercialCommand(cuenta.Id, Activa: false), CancellationToken.None);

        Assert.False(cuenta.Activa);
        Assert.Equal(EstadoCuentaComercial.Confirmada, cuenta.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Reactivar_vuelve_a_activar_una_cuenta_dada_de_baja()
    {
        var cuenta = Aceptada();
        cuenta.Desactivar();

        await CambiarEstado(AdminOperador()).Handle(
            new CambiarEstadoCuentaComercialCommand(cuenta.Id, Activa: true), CancellationToken.None);

        Assert.True(cuenta.Activa);
    }

    [Fact]
    public async Task Dar_de_baja_exige_administrador_del_operador_y_cuenta_propia()
    {
        var cuenta = Aceptada();
        var despachador = new SesionFalsa(OperadorId, null, Perfiles.Despachador, autenticado: true);
        var comando = new CambiarEstadoCuentaComercialCommand(cuenta.Id, Activa: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CambiarEstado(despachador).Handle(comando, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CambiarEstado(AdminComercio()).Handle(comando, CancellationToken.None));
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            CambiarEstado(AdminOperador(Guid.NewGuid())).Handle(comando, CancellationToken.None));
        Assert.True(cuenta.Activa);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task No_se_da_de_baja_una_solicitud_pendiente()
    {
        var solicitud = Pendiente();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CambiarEstado(AdminOperador()).Handle(
                new CambiarEstadoCuentaComercialCommand(solicitud.Id, Activa: false), CancellationToken.None));
    }

    // ----- Modificar comercio (RF 1) -----

    private readonly ComerciosEnMemoria _comercios = new();

    private ModificarComercioHandler Modificar(ITenantContext sesion) =>
        new(_cuentas, _comercios, _unidad, sesion, new ModificarComercioValidator());

    [Fact]
    public async Task Modificar_cambia_el_nombre_y_conserva_el_rut()
    {
        var cuenta = Aceptada();
        var comercio = Comercio.Crear(ComercioId, "211234560018", "Tienda");
        _comercios.Lista.Add(comercio);

        await Modificar(AdminOperador()).Handle(
            new ModificarComercioCommand(cuenta.Id, " Tienda Norte "), CancellationToken.None);

        Assert.Equal("Tienda Norte", comercio.Nombre);
        Assert.Equal("211234560018", comercio.Rut);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Modificar_valida_actor_nombre_y_estado_de_la_cuenta()
    {
        var aceptada = Aceptada();
        var pendiente = CuentaComercial.Solicitar(Guid.NewGuid(), OperadorId, Guid.NewGuid());
        _cuentas.Lista.Add(pendiente);
        _comercios.Lista.Add(Comercio.Crear(ComercioId, "211234560018", "Tienda"));
        var despachador = new SesionFalsa(OperadorId, null, Perfiles.Despachador, autenticado: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Modificar(despachador).Handle(new ModificarComercioCommand(aceptada.Id, "Otro"), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() =>
            Modificar(AdminOperador()).Handle(new ModificarComercioCommand(aceptada.Id, " "), CancellationToken.None));
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            Modificar(AdminOperador(Guid.NewGuid())).Handle(new ModificarComercioCommand(aceptada.Id, "Otro"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Modificar(AdminOperador()).Handle(new ModificarComercioCommand(pendiente.Id, "Otro"), CancellationToken.None));
        Assert.Equal(0, _unidad.Guardados);
    }

    // ----- Dobles de prueba -----

    private sealed class ComerciosEnMemoria : IComercioRepository
    {
        public List<Comercio> Lista { get; } = [];

        public Task<Comercio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(c => c.Id == id));
    }

    private sealed class SesionFalsa(Guid? operadorId, Guid? comercioId, string? perfil, bool autenticado)
        : ITenantContext
    {
        public Guid? UsuarioId => autenticado ? Guid.NewGuid() : null;
        public Guid? OperadorId => operadorId;
        public Guid? ComercioId => comercioId;
        public string? Perfil => perfil;
        public bool EstaAutenticado => autenticado;
    }

    private sealed class CuentasEnMemoria : ICuentaComercialRepository
    {
        public List<CuentaComercial> Lista { get; } = [];

        public Task<CuentaComercial?> ObtenerPorComercioAsync(
    Guid cuentaComercialId,
    Guid comercioId,
    CancellationToken ct) =>
    Task.FromResult(
        Lista.FirstOrDefault(c =>
            c.Id == cuentaComercialId &&
            c.ComercioId == comercioId));

        public Task<CuentaComercial?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(c => c.Id == id));

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
            Task.FromResult<IReadOnlyList<CuentaComercialDto>>(Lista
                .Where(c => c.ComercioId == comercioId)
                .Select(c => new CuentaComercialDto(c.Id, c.OperadorId, "", c.ComercioId, "", c.Activa, c.Estado))
                .ToList());
    }

    private sealed class OperadorActivoFalso : IOperadorActivo
    {
        public void SeleccionarOperador(Guid operadorId) { }
    }

    private sealed class OperadoresEnMemoria : IOperadorRepository
    {
        public List<Operador> Lista { get; } = [];

        public Task<IReadOnlyList<Operador>> ListarAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Operador>>(Lista.ToList());

        public Task<Operador?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(o => o.Id == id));
    }

    private sealed class UnidadDeTrabajoEnMemoria : IUnitOfWork
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
