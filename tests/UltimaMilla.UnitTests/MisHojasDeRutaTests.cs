using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Notificaciones.Commands.AvisarAsignacion;
using UltimaMilla.Application.Rutas;
using UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;
using UltimaMilla.Application.Rutas.Commands.DespacharMiHojaDeRuta;
using UltimaMilla.Application.Rutas.Queries.ObtenerMisHojasDeRuta;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Notificaciones;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.UnitTests;

/// <summary>Repartidor vinculado a un usuario, descarga de su hoja de ruta y despacho con verificación de carga (RF 17–19).</summary>
public class MisHojasDeRutaTests
{
    private static readonly DateTimeOffset Alta = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Alta.DateTime);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly VersionReglasOperativas _reglas;
    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;

    private readonly List<Envio> _envios = [];
    private readonly List<HojaDeRuta> _hojas = [];
    private readonly List<Repartidor> _repartidores = [];
    private readonly SesionFalsa _sesion;
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();
    private readonly ObtenerMisHojasDeRutaHandler _handler;
    private readonly DespacharMiHojaDeRutaHandler _despachar;

    public MisHojasDeRutaTests()
    {
        _reglas = VersionReglasOperativas.Crear(
            _operadorId,
            maximoIntentosEntrega: 3,
            horasMinimasEntreIntentos: 24,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 7,
            vigenciaDesde: Alta.AddDays(-1));

        _repartidor = Repartidor.Crear(_operadorId, "Ana Repartidora", "099111222");
        _repartidor.VincularUsuario(_usuarioId);
        _repartidores.Add(_repartidor);

        _vehiculo = Vehiculo.Crear(_operadorId, "STP1234", 100m, 10m);

        _sesion = new SesionFalsa { UsuarioId = _usuarioId, OperadorId = _operadorId };

        _handler = new ObtenerMisHojasDeRutaHandler(
            new RepartidoresEnMemoria(_repartidores),
            new HojasEnMemoria(_hojas),
            new EnviosEnMemoria(_envios),
            new RetirosEnMemoria(new List<Retiro>()),
            new CuentasEnMemoria(),
            _sesion);

        _despachar = new DespacharMiHojaDeRutaHandler(
            new RepartidoresEnMemoria(_repartidores),
            new HojasEnMemoria(_hojas),
            new EnviosEnMemoria(_envios),
            new RetirosEnMemoria(new List<Retiro>()),
            _sesion,
            new DespacharHojaDeRutaHandler(
                new HojasEnMemoria(_hojas),
                new EnviosEnMemoria(_envios),
                new RetirosEnMemoria(new List<Retiro>()),
                new ReglasEnMemoria(_reglas),
                _unidad,
                new RelojFijo(Alta.AddHours(1))));
    }

    // ----- Dominio: vínculo repartidor ↔ usuario -----

    [Fact]
    public void Un_repartidor_se_vincula_a_un_usuario()
    {
        var repartidor = Repartidor.Crear(_operadorId, "Luis", "099");
        var usuario = Guid.NewGuid();

        repartidor.VincularUsuario(usuario);

        Assert.Equal(usuario, repartidor.UsuarioId);
    }

    [Fact]
    public void No_se_puede_pisar_el_vinculo_con_otro_usuario()
    {
        Assert.Throws<InvalidOperationException>(() => _repartidor.VincularUsuario(Guid.NewGuid()));
        Assert.Equal(_usuarioId, _repartidor.UsuarioId);
    }

    [Fact]
    public void Desvincular_deja_al_repartidor_sin_cuenta()
    {
        _repartidor.DesvincularUsuario();

        Assert.Null(_repartidor.UsuarioId);
    }

    // ----- Consulta: mi hoja de ruta -----

    [Fact]
    public async Task Devuelve_las_paradas_en_orden_con_sus_bultos()
    {
        var hoja = CrearHoja(Hoy);
        var primero = AgregarEnvio(hoja, "PED-001", bultos: 2);
        AgregarEnvio(hoja, "PED-002", bultos: 1);

        var resultado = await _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None);

        var mia = Assert.Single(resultado);
        Assert.Equal("Ana Repartidora", mia.Repartidor);
        Assert.Equal(new[] { "PED-001", "PED-002" }, mia.Paradas.Select(p => p.ReferenciaExterna));
        Assert.Equal(primero.Bultos.Select(b => b.Identificacion).OrderBy(i => i), mia.Paradas[0].Bultos);
        Assert.Equal(2, mia.Paradas[0].Bultos.Count);
        // El detalle trae el Id de cada bulto, en el mismo orden (lo usa la entrega parcial, RF 32).
        Assert.Equal(
            primero.Bultos.OrderBy(b => b.Identificacion).Select(b => b.Id),
            mia.Paradas[0].DetalleBultos.Select(b => b.Id));
    }

    [Fact]
    public async Task No_devuelve_las_hojas_de_otro_dia()
    {
        var ayer = CrearHoja(Hoy.AddDays(-1));
        AgregarEnvio(ayer, "PED-VIEJO", bultos: 1);

        var resultado = await _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task No_devuelve_las_hojas_de_otro_repartidor()
    {
        var otro = Repartidor.Crear(_operadorId, "Otro", "098");
        _repartidores.Add(otro);
        var hojaAjena = HojaDeRuta.Crear(_operadorId, otro, _vehiculo, Hoy, 5);
        _hojas.Add(hojaAjena);
        AgregarEnvio(hojaAjena, "PED-AJENO", bultos: 1);

        var resultado = await _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Usuario_sin_repartidor_vinculado_da_no_encontrado()
    {
        _sesion.UsuarioId = Guid.NewGuid();

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None));
    }

    // ----- Verificación de la carga (RF 19) -----

    [Fact]
    public void Verificar_carga_separa_cargados_faltantes_y_sobrantes()
    {
        var resultado = VerificacionDeCarga.Verificar(
            ["PED-1-1", "PED-1-2", "PED-2-1"],
            ["ped-1-1 ", "PED-1-1", "PED-9-1"]);

        Assert.Equal(new[] { "PED-1-1" }, resultado.Cargados);
        Assert.Equal(new[] { "PED-1-2", "PED-2-1" }, resultado.Faltantes);
        Assert.Equal(new[] { "PED-9-1" }, resultado.Sobrantes);
        Assert.False(resultado.Completa);
    }

    // ----- Despacho desde la app -----

    [Fact]
    public async Task Con_todos_los_bultos_escaneados_despacha_y_quedan_en_transito()
    {
        var hoja = CrearHoja(Hoy);
        var envio = AgregarEnvio(hoja, "PED-001", bultos: 2);

        var cantidad = await _despachar.Handle(
            new DespacharMiHojaDeRutaCommand(hoja.Id, ["PED-001-1", "PED-001-2"]),
            CancellationToken.None);

        Assert.Equal(1, cantidad);
        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Si_falta_un_bulto_no_despacha_y_dice_cual()
    {
        var hoja = CrearHoja(Hoy);
        var envio = AgregarEnvio(hoja, "PED-001", bultos: 2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _despachar.Handle(new DespacharMiHojaDeRutaCommand(hoja.Id, ["PED-001-1"]), CancellationToken.None));

        Assert.Contains("faltan PED-001-2", ex.Message);
        Assert.Equal(EstadoEnvio.AsignadoARuta, envio.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Si_sobra_un_bulto_no_despacha_y_dice_cual()
    {
        var hoja = CrearHoja(Hoy);
        var envio = AgregarEnvio(hoja, "PED-001", bultos: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _despachar.Handle(new DespacharMiHojaDeRutaCommand(hoja.Id, ["PED-001-1", "PED-999-1"]), CancellationToken.None));

        Assert.Contains("sobran PED-999-1", ex.Message);
        Assert.Equal(EstadoEnvio.AsignadoARuta, envio.Estado);
    }

    [Fact]
    public async Task No_se_puede_despachar_la_hoja_de_otro_repartidor()
    {
        var otro = Repartidor.Crear(_operadorId, "Otro", "098");
        _repartidores.Add(otro);
        var hojaAjena = HojaDeRuta.Crear(_operadorId, otro, _vehiculo, Hoy, 5);
        _hojas.Add(hojaAjena);
        AgregarEnvio(hojaAjena, "PED-AJENO", bultos: 1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _despachar.Handle(new DespacharMiHojaDeRutaCommand(hojaAjena.Id, ["PED-AJENO-1"]), CancellationToken.None));
    }

    // ----- Push al repartidor cuando le asignan un envío (reasignación urgente) -----

    private AvisarAsignacionHandler Avisar(DispositivosEnMemoria dispositivos, NotificadorFalso notificador) =>
        new(new HojasEnMemoria(_hojas), new RepartidoresEnMemoria(_repartidores), new EnviosEnMemoria(_envios),
            dispositivos, notificador, _sesion, _unidad);

    [Fact]
    public async Task Al_asignar_un_envio_le_llega_un_push_al_telefono_del_repartidor_de_esa_hoja()
    {
        var hoja = CrearHoja(Hoy);
        var envio = AgregarEnvio(hoja, "PED-URG", bultos: 1);
        var dispositivos = new DispositivosEnMemoria();
        dispositivos.Lista.Add(DispositivoMovil.Registrar(_operadorId, _usuarioId, "token-ana", "Android", Alta));
        var notificador = new NotificadorFalso();

        var resultado = await Avisar(dispositivos, notificador).Handle(new AvisarAsignacionCommand(envio.Id), CancellationToken.None);

        Assert.Equal(1, resultado.Enviadas);
        var (token, aviso) = Assert.Single(notificador.Enviados);
        Assert.Equal("token-ana", token);
        Assert.Contains("PED-URG", aviso.Cuerpo);
        Assert.Equal(envio.Id.ToString(), aviso.Datos!["envioId"]);
    }

    [Fact]
    public async Task No_le_avisa_a_otro_repartidor()
    {
        var otro = Repartidor.Crear(_operadorId, "Otro", "098");
        var usuarioOtro = Guid.NewGuid();
        otro.VincularUsuario(usuarioOtro);
        _repartidores.Add(otro);
        var hojaAjena = HojaDeRuta.Crear(_operadorId, otro, _vehiculo, Hoy, 5);
        _hojas.Add(hojaAjena);
        var envio = AgregarEnvio(hojaAjena, "PED-AJENO", bultos: 1);

        var dispositivos = new DispositivosEnMemoria();
        dispositivos.Lista.Add(DispositivoMovil.Registrar(_operadorId, _usuarioId, "token-ana", "Android", Alta));
        dispositivos.Lista.Add(DispositivoMovil.Registrar(_operadorId, usuarioOtro, "token-otro", "Android", Alta));
        var notificador = new NotificadorFalso();

        await Avisar(dispositivos, notificador).Handle(new AvisarAsignacionCommand(envio.Id), CancellationToken.None);

        Assert.Equal("token-otro", Assert.Single(notificador.Enviados).Token);
    }

    [Fact]
    public async Task Un_token_que_Firebase_ya_no_reconoce_se_da_de_baja()
    {
        var hoja = CrearHoja(Hoy);
        var envio = AgregarEnvio(hoja, "PED-URG", bultos: 1);
        var dispositivos = new DispositivosEnMemoria();
        dispositivos.Lista.Add(DispositivoMovil.Registrar(_operadorId, _usuarioId, "token-viejo", "Android", Alta));
        var notificador = new NotificadorFalso();
        notificador.Invalidos.Add("token-viejo");

        var resultado = await Avisar(dispositivos, notificador).Handle(new AvisarAsignacionCommand(envio.Id), CancellationToken.None);

        Assert.Equal(1, resultado.TokensEliminados);
        Assert.Empty(dispositivos.Lista);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Si_el_envio_ya_no_esta_en_ninguna_hoja_no_avisa()
    {
        var dispositivos = new DispositivosEnMemoria();
        dispositivos.Lista.Add(DispositivoMovil.Registrar(_operadorId, _usuarioId, "token-ana", "Android", Alta));
        var notificador = new NotificadorFalso();

        var resultado = await Avisar(dispositivos, notificador).Handle(new AvisarAsignacionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, resultado.Enviadas);
        Assert.Empty(notificador.Enviados);
    }

    // ----- Ayudas -----

    private HojaDeRuta CrearHoja(DateOnly fecha)
    {
        var hoja = HojaDeRuta.Crear(_operadorId, _repartidor, _vehiculo, fecha, 5);
        _hojas.Add(hoja);
        return hoja;
    }

    private Envio AgregarEnvio(HojaDeRuta hoja, string referencia, int bultos)
    {
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), _operadorId, Guid.NewGuid());

        var envio = Envio.Crear(
            cuenta,
            referencia,
            Modalidad.Estandar,
            new Destinatario("Destinatario Test", "099123456"),
            new Direccion("Calle Test", "123", "Montevideo", "Montevideo"),
            Enumerable.Range(0, bultos).Select(_ => new NuevoBulto(1m, 10, 10, 10)).ToList(),
            Alta);

        envio.AsignarReglasOperativas(_reglas.Id);
        envio.Aplicar(DisparadorEnvio.RecibirEnDeposito, _reglas, Alta, "Prueba", "OperarioTest");

        hoja.AgregarEnvio(envio);
        envio.Aplicar(DisparadorEnvio.AsignarARuta, _reglas, Alta, "Backoffice", "Despachador");

        _envios.Add(envio);
        return envio;
    }

    // ----- Dobles de prueba -----

    private sealed class SesionFalsa : ITenantContext
    {
        public Guid? UsuarioId { get; set; }
        public Guid? OperadorId { get; set; }
        public Guid? ComercioId => null;
        public string? Perfil => Perfiles.Repartidor;
        public bool EstaAutenticado => UsuarioId is not null;
    }

    private sealed class RepartidoresEnMemoria(List<Repartidor> lista) : IRepartidorRepository
    {
        public Task<Repartidor?> ObtenerAsync(Guid operadorId, Guid repartidorId, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(r => r.OperadorId == operadorId && r.Id == repartidorId));

        public Task<Repartidor?> ObtenerPorUsuarioAsync(Guid operadorId, Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(r =>
                r.OperadorId == operadorId && r.UsuarioId == usuarioId && r.Activo));

        public Task<IReadOnlyList<Repartidor>> ListarActivosAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Repartidor>>(lista.Where(r => r.OperadorId == operadorId && r.Activo).ToList());
    }

    private sealed class HojasEnMemoria(List<HojaDeRuta> lista) : IHojaDeRutaRepository
    {
        public Task AgregarAsync(HojaDeRuta hoja, CancellationToken ct)
        {
            lista.Add(hoja);
            return Task.CompletedTask;
        }

        public Task<HojaDeRuta?> ObtenerAsync(Guid operadorId, Guid hojaId, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(h => h.Id == hojaId && h.OperadorId == operadorId));

        public Task<IReadOnlyList<HojaDeRuta>> ListarDelRepartidorAsync(
            Guid operadorId, Guid repartidorId, DateOnly fecha, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(lista.Where(h =>
                h.OperadorId == operadorId && h.RepartidorId == repartidorId && h.Fecha == fecha).ToList());

        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(lista.Where(h => h.OperadorId == operadorId).ToList());

        public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync( Guid operadorId, Guid envioId, CancellationToken ct) =>
           Task.FromResult(lista.SingleOrDefault(h => h.OperadorId == operadorId && h.Paradas.Any(p => p.EnvioId == envioId && p.Activa)));
    
        public Task<HojaDeRuta?> ObtenerPorRetiroEnEntregaActivoAsync(
            Guid operadorId,
            Guid retiroId,
            CancellationToken ct)
        {
            return Task.FromResult<HojaDeRuta?>(null);
        }
    }

    private sealed class ReglasEnMemoria(VersionReglasOperativas reglas) : IVersionReglasOperativasRepository
    {
        public Task<VersionReglasOperativas?> ObtenerVigenteAsync(Guid operadorId, DateTimeOffset fecha, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(reglas.OperadorId == operadorId ? reglas : null);

        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(Guid operadorId, Guid versionId, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(
                reglas.OperadorId == operadorId && reglas.Id == versionId ? reglas : null);

        public Task<VersionReglasOperativas?> ObtenerVigenteParaActualizarAsync(
            Guid operadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(
                reglas.OperadorId == operadorId && reglas.EstaVigente(fecha)
                    ? reglas
                    : null);

        public Task AgregarAsync(
            VersionReglasOperativas version,
            CancellationToken ct) =>
            throw new NotSupportedException(
                "Este repositorio de prueba no permite agregar versiones.");
    }

    private sealed class UnidadDeTrabajoEnMemoria : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(
            Func<CancellationToken, Task> operacion,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(operacion);
            return operacion(ct);
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class CuentasEnMemoria : ICuentaComercialRepository
    {
        public Task<CuentaComercial?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<CuentaComercial?>(null);

        public Task<IReadOnlyList<UltimaMilla.Application.CuentasComerciales.CuentaComercialDto>>
            ListarResumenAsync(
                Guid operadorId,
                Guid? cuentaComercialId,
                CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CuentaComercial?> ObtenerPorOperadorYComercioAsync(
            Guid operadorId,
            Guid comercioId,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task AgregarAsync(
            CuentaComercial cuenta,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes.SolicitudPendienteDto>>
            ListarSolicitudesPendientesAsync(
                Guid operadorId,
                CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UltimaMilla.Application.CuentasComerciales.CuentaComercialDto>>
            ListarPorComercioAsync(
                Guid comercioId,
                CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class EnviosEnMemoria(List<Envio> lista) : IEnvioRepository
    {
        public Task AgregarAsync(Envio envio, CancellationToken ct)
        {
            lista.Add(envio);
            return Task.CompletedTask;
        }


        public Task<IReadOnlyList<Envio>> ListarPorCuentasComercialesAsync(
            IReadOnlyCollection<Guid> cuentasComercialesIds,
            CancellationToken ct)
        {
            IReadOnlyList<Envio> resultado = lista
                .Where(e => cuentasComercialesIds.Contains(e.CuentaComercialId))
                .ToList();

            return Task.FromResult(resultado);
        }

        public Task<Envio?> ObtenerPorComercioAsync(
            Guid envioId,
            Guid comercioId,
            CancellationToken ct)
        {
            // Esta prueba no utiliza esta consulta.
            throw new NotSupportedException(
                "Esta prueba no utiliza la consulta de envíos por comercio.");
        }


        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(e => e.Id == id));

        public Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct) =>
            Task.FromResult(lista.Any(e => e.CuentaComercialId == cuentaComercialId && e.ReferenciaExterna == referenciaExterna));

        public Task<IReadOnlyList<Envio>> ListarAsync(Guid operadorId, Guid? cuentaComercialId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(lista.Where(e => e.OperadorId == operadorId).ToList());

        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync( Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>( lista.Where(e => e.OperadorId == operadorId).ToList());

            public Task<Envio?> ObtenerPorTokenSeguimientoAsync(
    string tokenHash,
    CancellationToken ct)
{
    return Task.FromResult<Envio?>(null);
}
    }

    private sealed class DispositivosEnMemoria : IDispositivoMovilRepository
    {
        public List<DispositivoMovil> Lista { get; } = [];

        public Task<DispositivoMovil?> ObtenerPorTokenAsync(string token, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(d => d.Token == token));

        public Task<IReadOnlyList<DispositivoMovil>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DispositivoMovil>>(Lista.Where(d => d.UsuarioId == usuarioId).ToList());

        public Task AgregarAsync(DispositivoMovil dispositivo, CancellationToken ct)
        {
            Lista.Add(dispositivo);
            return Task.CompletedTask;
        }

        public void Eliminar(DispositivoMovil dispositivo) => Lista.Remove(dispositivo);
    }

    private sealed class NotificadorFalso : INotificadorPush
    {
        public HashSet<string> Invalidos { get; } = [];
        public List<(string Token, NotificacionPush Notificacion)> Enviados { get; } = [];

        public Task<ResultadoPush> EnviarAsync(string token, NotificacionPush notificacion, CancellationToken ct)
        {
            if (Invalidos.Contains(token))
                return Task.FromResult(ResultadoPush.TokenInvalido);

            Enviados.Add((token, notificacion));
            return Task.FromResult(ResultadoPush.Enviado);
        }
    }
}
