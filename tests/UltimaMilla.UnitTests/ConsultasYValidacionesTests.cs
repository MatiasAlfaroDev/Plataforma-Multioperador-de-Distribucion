using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Application.Avisos.Commands.CrearSuscripcion;
using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;
using UltimaMilla.Application.Retiros.Commands.CrearRetiro;
using UltimaMilla.Application.Retiros.Queries;
using UltimaMilla.Application.Rutas.Commands.CrearHojaDeRuta;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.UnitTests;

public class ConsultasYValidacionesTests
{
    private static readonly Guid IdOperador = Guid.NewGuid();
    private static readonly Guid IdComercio = Guid.NewGuid();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // ===== ConsultarRetirosHandler =====

    private readonly RepositorioRetiros _retiros = new();
    private readonly RepositorioCuentas _cuentas = new();
    private readonly OperadorActivoFalso _activo = new();

    private ConsultarRetirosHandler Consultas(ITenantContext sesion) =>
        new(_retiros, _cuentas, sesion, _activo);

    private static Retiro NuevoRetiro(Guid operadorId, Guid cuentaId, DateTimeOffset solicitadoEn) =>
        Retiro.Crear(
            operadorId,
            cuentaId,
            "Cambio de talle",
            new Direccion("Origen", "100", "San José de Mayo", "San José"),
            new Direccion("Comercio", "200", "San José de Mayo", "San José"),
            DateOnly.FromDateTime(solicitadoEn.ToOffset(TimeSpan.FromHours(-3)).DateTime),
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [BultoRetiro.Crear("RET-001", 5m, 20, 20, 20)],
            solicitadoEn);

    private static CuentaComercialDto Cuenta(
        Guid id, Guid operadorId, bool activa = true,
        EstadoCuentaComercial estado = EstadoCuentaComercial.Confirmada) =>
        new(id, operadorId, "Operador", IdComercio, "Comercio", activa, estado);

    [Fact]
    public async Task El_operador_lista_solo_los_retiros_de_su_operador()
    {
        var propio = NuevoRetiro(IdOperador, Guid.NewGuid(), Ahora);
        var ajeno = NuevoRetiro(Guid.NewGuid(), Guid.NewGuid(), Ahora);
        _retiros.Lista.AddRange([propio, ajeno]);

        var resultado = await Consultas(Sesion(operadorId: IdOperador, perfil: Perfiles.Operador))
            .ListarAsync(CancellationToken.None);

        var dto = Assert.Single(resultado);
        Assert.Equal(propio.Id, dto.Id);
        Assert.Equal("RET-001", Assert.Single(dto.Bultos).Identificacion);
        Assert.Equal("San José", dto.DomicilioRetiro.Departamento);
        Assert.Equal(nameof(EstadoRetiro.Solicitado), dto.Estado);
    }

    [Fact]
    public async Task El_comercio_lista_los_retiros_de_sus_cuentas_activas_ordenados_por_fecha()
    {
        var otroOperador = Guid.NewGuid();
        var cuentaA = Cuenta(Guid.NewGuid(), IdOperador);
        var cuentaB = Cuenta(Guid.NewGuid(), otroOperador);
        var cuentaInactiva = Cuenta(Guid.NewGuid(), Guid.NewGuid(), activa: false);
        _cuentas.PorComercio.AddRange([cuentaA, cuentaB, cuentaInactiva]);

        var viejo = NuevoRetiro(IdOperador, cuentaA.Id, Ahora);
        var nuevo = NuevoRetiro(otroOperador, cuentaB.Id, Ahora.AddMinutes(5));
        var deLaInactiva = NuevoRetiro(cuentaInactiva.OperadorId, cuentaInactiva.Id, Ahora);
        _retiros.Lista.AddRange([viejo, nuevo, deLaInactiva]);

        var resultado = await Consultas(Sesion(comercioId: IdComercio, perfil: Perfiles.Comercio))
            .ListarAsync(CancellationToken.None);

        Assert.Equal(new[] { nuevo.Id, viejo.Id }, resultado.Select(r => r.Id).ToArray());
        Assert.Contains(IdOperador, _activo.Seleccionados);
        Assert.Contains(otroOperador, _activo.Seleccionados);
        Assert.DoesNotContain(cuentaInactiva.OperadorId, _activo.Seleccionados);
    }

    [Fact]
    public async Task Listar_retiros_exige_sesion_y_perfil_con_acceso()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Consultas(Sesion(autenticado: false)).ListarAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Consultas(Sesion(operadorId: IdOperador, perfil: Perfiles.Repartidor)).ListarAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Consultas(Sesion(perfil: Perfiles.Operador)).ListarAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Consultas(Sesion(perfil: Perfiles.Comercio)).ListarAsync(CancellationToken.None));
    }

    [Fact]
    public async Task El_operador_obtiene_un_retiro_propio_y_no_uno_ajeno()
    {
        var propio = NuevoRetiro(IdOperador, Guid.NewGuid(), Ahora);
        var ajeno = NuevoRetiro(Guid.NewGuid(), Guid.NewGuid(), Ahora);
        _retiros.Lista.AddRange([propio, ajeno]);
        var handler = Consultas(Sesion(operadorId: IdOperador, perfil: Perfiles.AdministradorOperador));

        Assert.Equal(propio.Id, (await handler.ObtenerAsync(propio.Id, CancellationToken.None))!.Id);
        Assert.Null(await handler.ObtenerAsync(ajeno.Id, CancellationToken.None));
        Assert.Null(await handler.ObtenerAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task El_comercio_obtiene_solo_retiros_de_sus_propias_cuentas()
    {
        var cuenta = Cuenta(Guid.NewGuid(), IdOperador);
        _cuentas.PorComercio.Add(cuenta);
        var propio = NuevoRetiro(IdOperador, cuenta.Id, Ahora);
        var deOtraCuenta = NuevoRetiro(IdOperador, Guid.NewGuid(), Ahora);
        _retiros.Lista.AddRange([propio, deOtraCuenta]);
        var handler = Consultas(Sesion(comercioId: IdComercio, perfil: Perfiles.AdministradorComercio));

        Assert.Equal(propio.Id, (await handler.ObtenerAsync(propio.Id, CancellationToken.None))!.Id);
        Assert.Null(await handler.ObtenerAsync(deOtraCuenta.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Obtener_retiro_valida_sesion_identificador_y_perfil()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Consultas(Sesion(autenticado: false)).ObtenerAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Consultas(Sesion(operadorId: IdOperador, perfil: Perfiles.Operador))
                .ObtenerAsync(Guid.Empty, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Consultas(Sesion(operadorId: IdOperador, perfil: Perfiles.Repartidor))
                .ObtenerAsync(Guid.NewGuid(), CancellationToken.None));
    }

    // ===== CrearRetiroValidator =====

    private static CrearRetiroCommand RetiroValido() =>
        new(Guid.NewGuid(),
            "Cambio de talle",
            new DireccionRetiroCommand("18 de Julio", "1234", "San José de Mayo", "San José"),
            new DireccionRetiroCommand("Rivera", "55", "San José de Mayo", "San José"),
            new DateOnly(2026, 11, 20),
            new TimeOnly(9, 0),
            new TimeOnly(12, 0),
            [new BultoRetiroCommand("RET-001", 5m, 20, 30, 40)]);

    private static bool Falla(CrearRetiroCommand comando, string propiedad) =>
        new CrearRetiroValidator().Validate(comando).Errors
            .Any(e => e.PropertyName.StartsWith(propiedad, StringComparison.Ordinal));

    [Fact]
    public void Un_retiro_completo_es_valido()
    {
        Assert.True(new CrearRetiroValidator().Validate(RetiroValido()).IsValid);
    }

    [Fact]
    public void El_retiro_exige_cuenta_motivo_y_fecha()
    {
        Assert.True(Falla(RetiroValido() with { CuentaComercialId = Guid.Empty }, "CuentaComercialId"));
        Assert.True(Falla(RetiroValido() with { Motivo = "" }, "Motivo"));
        Assert.True(Falla(RetiroValido() with { Motivo = new string('x', 501) }, "Motivo"));
        Assert.True(Falla(RetiroValido() with { Fecha = default }, "Fecha"));
    }

    [Fact]
    public void El_retiro_exige_domicilios_completos()
    {
        Assert.True(Falla(RetiroValido() with { DomicilioRetiro = null! }, "DomicilioRetiro"));
        Assert.True(Falla(RetiroValido() with { DomicilioDestino = null! }, "DomicilioDestino"));
        
        var sinCalle = new DireccionRetiroCommand("", "1", "San José de Mayo", "San José");
        Assert.True(Falla(RetiroValido() with { DomicilioRetiro = sinCalle }, "DomicilioRetiro.Calle"));

        var sinLocalidad = new DireccionRetiroCommand("Rivera", "1", "", "San José");
        Assert.True(Falla(RetiroValido() with { DomicilioDestino = sinLocalidad }, "DomicilioDestino.Localidad"));

        var sinDepartamento = new DireccionRetiroCommand("Rivera", "1", "Libertad", "");
        Assert.True(Falla(RetiroValido() with { DomicilioRetiro = sinDepartamento }, "DomicilioRetiro.Departamento"));
    }

    [Fact]
    public void La_hora_desde_debe_ser_anterior_a_la_hora_hasta()
    {
        Assert.True(Falla(RetiroValido() with { HoraDesde = new TimeOnly(12, 0), HoraHasta = new TimeOnly(12, 0) }, "HoraDesde"));
        Assert.True(Falla(RetiroValido() with { HoraDesde = new TimeOnly(15, 0), HoraHasta = new TimeOnly(9, 0) }, "HoraDesde"));
    }

    [Fact]
    public void El_retiro_exige_al_menos_un_bulto()
    {
        Assert.True(Falla(RetiroValido() with { Bultos = [] }, "Bultos"));
    }

    [Theory]
    [InlineData("", 5, 20, 20, 20)]
    [InlineData("A", 0, 20, 20, 20)]
    [InlineData("A", 1001, 20, 20, 20)]
    [InlineData("A", 5, 0, 20, 20)]
    [InlineData("A", 5, 20, 501, 20)]
    [InlineData("A", 5, 20, 20, 0)]
    public void Cada_bulto_respeta_sus_limites(string identificacion, decimal peso, int alto, int ancho, int profundidad)
    {
        var comando = RetiroValido() with
        {
            Bultos = [new BultoRetiroCommand(identificacion, peso, alto, ancho, profundidad)]
        };

        Assert.True(Falla(comando, "Bultos[0]"));
    }

    [Fact]
    public void No_se_admiten_bultos_con_identificacion_repetida()
    {
        var comando = RetiroValido() with
        {
            Bultos =
            [
                new BultoRetiroCommand("RET-001", 5m, 20, 20, 20),
                new BultoRetiroCommand(" ret-001 ", 3m, 10, 10, 10)
            ]
        };

        var resultado = new CrearRetiroValidator().Validate(comando);

        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("repetidas"));
    }

    // ===== CrearSuscripcionValidator =====

    private static CrearSuscripcionCommand Suscripcion(string url = "https://comercio.test/avisos", params string[] tipos) =>
        new(null, url, tipos.Length == 0 ? [TiposAviso.EnvioEntregado] : tipos);

    [Fact]
    public void Una_suscripcion_con_url_y_tipos_conocidos_es_valida()
    {
        var comando = Suscripcion(tipos: [TiposAviso.EnvioEntregado, TiposAviso.RetiroRetirado]);

        Assert.True(new CrearSuscripcionValidator().Validate(comando).IsValid);
    }

    [Fact]
    public void La_suscripcion_rechaza_url_vacia_o_demasiado_larga()
    {
        var validador = new CrearSuscripcionValidator();

        Assert.False(validador.Validate(Suscripcion(url: "")).IsValid);
        Assert.False(validador.Validate(Suscripcion(url: new string('a', SuscripcionAviso.LargoMaximoUrl + 1))).IsValid);
    }

    [Fact]
    public void La_suscripcion_exige_tipos_y_que_sean_conocidos()
    {
        var validador = new CrearSuscripcionValidator();

        Assert.False(validador.Validate(new CrearSuscripcionCommand(null, "https://comercio.test/a", [])).IsValid);
        Assert.False(validador.Validate(Suscripcion(tipos: ["evento.inventado"])).IsValid);
    }

    // ===== AlcanceAvisos =====

    [Fact]
    public async Task El_operador_ve_los_avisos_de_todo_su_operador()
    {
        var alcance = await AlcanceAvisos.ResolverAsync(
            Sesion(operadorId: IdOperador, perfil: Perfiles.AdministradorOperador), _cuentas, CancellationToken.None);

        Assert.Equal(IdOperador, alcance.OperadorId);
        Assert.Null(alcance.Cuentas);
        Assert.Equal(IdOperador, alcance.OperadorDeCuenta(Guid.NewGuid()));
    }

    [Fact]
    public async Task El_comercio_ve_solo_los_avisos_de_sus_cuentas_confirmadas()
    {
        var otroOperador = Guid.NewGuid();
        var confirmada = Cuenta(Guid.NewGuid(), otroOperador);
        var pendiente = Cuenta(Guid.NewGuid(), IdOperador, activa: false, estado: EstadoCuentaComercial.Pendiente);
        _cuentas.PorComercio.AddRange([confirmada, pendiente]);

        var alcance = await AlcanceAvisos.ResolverAsync(
            Sesion(comercioId: IdComercio, perfil: Perfiles.Comercio), _cuentas, CancellationToken.None);

        Assert.Null(alcance.OperadorId);
        Assert.Equal(otroOperador, alcance.OperadorDeCuenta(confirmada.Id));
        Assert.Null(alcance.OperadorDeCuenta(pendiente.Id));
    }

    [Fact]
    public async Task El_alcance_de_avisos_se_rechaza_sin_sesion_operador_comercio_o_permiso()
    {
        foreach (var sesion in new[]
                 {
                     Sesion(autenticado: false),
                     Sesion(perfil: Perfiles.Operador),
                     Sesion(operadorId: IdOperador, perfil: Perfiles.Comercio),
                     Sesion(operadorId: IdOperador, perfil: Perfiles.Repartidor)
                 })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                AlcanceAvisos.ResolverAsync(sesion, _cuentas, CancellationToken.None));
        }
    }

    // ===== ListarCuentasComerciales / ListarSolicitudesPendientes =====

    [Fact]
    public async Task El_comercio_lista_sus_cuentas()
    {
        var cuenta = Cuenta(Guid.NewGuid(), IdOperador);
        _cuentas.PorComercio.Add(cuenta);
        var handler = new ListarCuentasComercialesHandler(
            _cuentas, Sesion(comercioId: IdComercio, perfil: Perfiles.AdministradorComercio));

        var resultado = await handler.Handle(new ListarCuentasComercialesQuery(), CancellationToken.None);

        Assert.Equal(cuenta.Id, Assert.Single(resultado).Id);
        Assert.Equal(IdComercio, _cuentas.ComercioConsultado);
    }

    [Fact]
    public async Task El_operador_lista_el_resumen_de_sus_cuentas()
    {
        var cuenta = Cuenta(Guid.NewGuid(), IdOperador);
        _cuentas.Resumen.Add(cuenta);
        var handler = new ListarCuentasComercialesHandler(
            _cuentas, Sesion(operadorId: IdOperador, perfil: Perfiles.AdministradorOperador));

        var resultado = await handler.Handle(new ListarCuentasComercialesQuery(), CancellationToken.None);

        Assert.Equal(cuenta.Id, Assert.Single(resultado).Id);
        Assert.Equal(IdOperador, _cuentas.OperadorConsultado);
    }

    [Fact]
    public async Task Listar_cuentas_exige_sesion_comercio_asociado_y_perfil_valido()
    {
        var sinSesion = new ListarCuentasComercialesHandler(_cuentas, Sesion(autenticado: false));
        var sinComercio = new ListarCuentasComercialesHandler(_cuentas, Sesion(perfil: Perfiles.AdministradorComercio));
        var sinPermiso = new ListarCuentasComercialesHandler(
            _cuentas, Sesion(operadorId: IdOperador, perfil: Perfiles.Repartidor));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sinSesion.Handle(new ListarCuentasComercialesQuery(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sinComercio.Handle(new ListarCuentasComercialesQuery(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sinPermiso.Handle(new ListarCuentasComercialesQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task El_administrador_del_operador_lista_las_solicitudes_pendientes()
    {
        var pendiente = new SolicitudPendienteDto(Guid.NewGuid(), IdComercio, "Tienda", Ahora);
        _cuentas.Pendientes.Add(pendiente);
        var handler = new ListarSolicitudesPendientesHandler(
            _cuentas, Sesion(operadorId: IdOperador, perfil: Perfiles.AdministradorOperador));

        var resultado = await handler.Handle(new ListarSolicitudesPendientesQuery(), CancellationToken.None);

        Assert.Equal(pendiente.Id, Assert.Single(resultado).Id);
        Assert.Equal(IdOperador, _cuentas.OperadorConsultado);
    }

    [Fact]
    public async Task Las_solicitudes_pendientes_exigen_sesion_y_administrador_del_operador()
    {
        var sinSesion = new ListarSolicitudesPendientesHandler(_cuentas, Sesion(autenticado: false));
        var sinPerfil = new ListarSolicitudesPendientesHandler(
            _cuentas, Sesion(operadorId: IdOperador, perfil: Perfiles.Operador));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sinSesion.Handle(new ListarSolicitudesPendientesQuery(), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sinPerfil.Handle(new ListarSolicitudesPendientesQuery(), CancellationToken.None));
    }

    // ===== CrearHojaDeRutaCommand =====

    [Fact]
    public void Los_retiros_devoluciones_y_entregas_de_la_hoja_de_ruta_son_opcionales()
    {
        var envio = Guid.NewGuid();

        var comando = new CrearHojaDeRutaCommand(
            IdOperador, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 20),
            new TimeOnly(8, 0), new TimeOnly(9, 0), new TimeOnly(18, 0), 10, [envio]);

        Assert.Equal(envio, Assert.Single(comando.EnviosIds));
        Assert.Null(comando.RetirosIds);
        Assert.Null(comando.DevolucionesIds);
        Assert.Null(comando.EntregasRetirosIds);
    }

    // ===== Dobles de prueba =====

    private static SesionDePrueba Sesion(
        Guid? operadorId = null,
        Guid? comercioId = null,
        string? perfil = null,
        bool autenticado = true) =>
        new(operadorId, comercioId, perfil, autenticado);

    private sealed class SesionDePrueba(Guid? operadorId, Guid? comercioId, string? perfil, bool autenticado)
        : ITenantContext
    {
        public Guid? UsuarioId => autenticado ? Guid.NewGuid() : null;
        public Guid? OperadorId => operadorId;
        public Guid? ComercioId => comercioId;
        public string? Perfil => perfil;
        public bool EstaAutenticado => autenticado;
    }

    private sealed class OperadorActivoFalso : IOperadorActivo
    {
        public List<Guid> Seleccionados { get; } = [];

        public void SeleccionarOperador(Guid operadorId) => Seleccionados.Add(operadorId);
    }

    private sealed class RepositorioRetiros : IRetiroRepository
    {
        public List<Retiro> Lista { get; } = [];

        public Task AgregarAsync(Retiro retiro, CancellationToken ct)
        {
            Lista.Add(retiro);
            return Task.CompletedTask;
        }

        public Task<Retiro?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Retiro>> ListarAsync(Guid operadorId, Guid? cuentaComercialId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>(Lista
                .Where(r => r.OperadorId == operadorId &&
                            (!cuentaComercialId.HasValue || r.CuentaComercialId == cuentaComercialId.Value))
                .ToList());

        public Task<IReadOnlyList<Retiro>> ListarParaPlanificacionAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>([]);

        public Task<IReadOnlyList<Retiro>> ListarParaEntregaComercioAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>([]);
    }

    private sealed class RepositorioCuentas : ICuentaComercialRepository
    {
        public List<CuentaComercialDto> PorComercio { get; } = [];
        public List<CuentaComercialDto> Resumen { get; } = [];
        public List<SolicitudPendienteDto> Pendientes { get; } = [];
        public Guid? ComercioConsultado { get; private set; }
        public Guid? OperadorConsultado { get; private set; }

        public Task<CuentaComercial?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<CuentaComercial?>(null);

        public Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(
            Guid operadorId, Guid? cuentaComercialId, CancellationToken ct)
        {
            OperadorConsultado = operadorId;
            return Task.FromResult<IReadOnlyList<CuentaComercialDto>>(Resumen.ToList());
        }

        public Task<CuentaComercial?> ObtenerPorOperadorYComercioAsync(
            Guid operadorId, Guid comercioId, CancellationToken ct) =>
            Task.FromResult<CuentaComercial?>(null);

        public Task AgregarAsync(CuentaComercial cuenta, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<SolicitudPendienteDto>> ListarSolicitudesPendientesAsync(
            Guid operadorId, CancellationToken ct)
        {
            OperadorConsultado = operadorId;
            return Task.FromResult<IReadOnlyList<SolicitudPendienteDto>>(Pendientes.ToList());
        }

        public Task<IReadOnlyList<CuentaComercialDto>> ListarPorComercioAsync(Guid comercioId, CancellationToken ct)
        {
            ComercioConsultado = comercioId;
            return Task.FromResult<IReadOnlyList<CuentaComercialDto>>(
                PorComercio.Where(c => c.ComercioId == comercioId).ToList());
        }
    }
}
