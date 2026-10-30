
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Application.Abstractions.Caching;

namespace UltimaMilla.UnitTests;

public class RegistrarEventoMovilHandlerTests
{
    private static readonly DateTimeOffset Alta =
        new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly EnviosEnMemoria _envios = new();
    private readonly EventosEnMemoria _eventos = new();
    private readonly ReglasEnMemoria _reglas = new();
    private readonly MotivosEnMemoria _motivos = new();
    private readonly PublicadorEnMemoria _publicador = new();
    private readonly UnidadDeTrabajoEnMemoria _unidadDeTrabajo = new();

    private readonly RegistrarEventoMovilHandler _handler;
    private readonly Envio _envio;
    private readonly VersionReglasOperativas _versionReglas;
    private readonly MotivoNoEntrega _motivo;
    private readonly HojasEnMemoria _hojas = new();
    private readonly HojaDeRuta _hoja;

    public RegistrarEventoMovilHandlerTests()
    {
        var operadorId = Guid.NewGuid();

        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            operadorId,
            Guid.NewGuid());

        _versionReglas = VersionReglasOperativas.Crear(
            operadorId,
            maximoIntentosEntrega: 3,
            horasMinimasEntreIntentos: 1,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 5,
            vigenciaDesde: Alta.AddDays(-1));

        _reglas.Lista.Add(_versionReglas);

        _motivo = MotivoNoEntrega.Crear(
            operadorId,
            _versionReglas.Id,
            "AUSENTE",
            "Destinatario ausente",
            requiereDevolucion: false);

        _motivos.Lista.Add(_motivo);

        _envio = Envio.Crear(
            cuenta,
            "PED-200",
            Modalidad.Estandar,
            new Destinatario("Ana Pérez", null),
            new Direccion(
                "Av. Italia",
                "1234",
                "Montevideo",
                "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10)],
            Alta);

        _envio.AsignarReglasOperativas(_versionReglas.Id);

        // Preparamos un envío real en estado EnTransito
        // usando las transiciones del dominio.
        _envio.Aplicar(
            DisparadorEnvio.RecibirEnDeposito,
            _versionReglas,
            Alta.AddMinutes(10),
            "Backoffice",
            "Operario");

        var repartidor = Repartidor.Crear(
            operadorId,
            "Repartidor de prueba",
            "099123456");

        var vehiculo = Vehiculo.Crear(
            operadorId,
            "ABC1234",
            capacidadPesoKg: 1000m,
            capacidadVolumenM3: 10m);

        _hoja = HojaDeRuta.Crear(
            operadorId,
            repartidor,
            vehiculo,
            DateOnly.FromDateTime(Alta.Date),
            maximoParadas: 10);

        _hoja.AgregarEnvio(_envio);

        _hojas.Lista.Add(_hoja);

        _envio.Aplicar(
            DisparadorEnvio.AsignarARuta,
            _versionReglas,
            Alta.AddMinutes(20),
            "Backoffice",
            "Despachador");

        _envio.Aplicar(
            DisparadorEnvio.Despachar,
            _versionReglas,
            Alta.AddMinutes(30),
            "Mobile",
            "Repartidor");

        _envios.Lista.Add(_envio);

        _handler = new RegistrarEventoMovilHandler(
            _envios,
            _hojas,
            _eventos,
            _reglas,
            _motivos,
            _unidadDeTrabajo,
            _publicador,
            new RegistrarEventoMovilValidator(),
            new RelojFijo(Alta.AddHours(5)),
            new CacheEnMemoria());
    }

    private RegistrarEventoMovilCommand Entrega(
        Guid idEvento,
        string receptor = "Ana") =>
        new(
            _envio.Id,
            idEvento,
            "Entregado",
            null,
            receptor,
            Alta.AddHours(2),
            ReferenciaFirma: "firma/prueba-001",
            Latitud: -34.90m,
            Longitud: -56.16m);

    private RegistrarEventoMovilCommand Fallo(
        Guid idEvento) =>
        new(
            _envio.Id,
            idEvento,
            "NoEntregado",
            _motivo.Descripcion,
            null,
            Alta.AddHours(2),
            EvidenciaFallo: "evidencia/intento-001");

    [Fact]
    public async Task Entrega_valida_cambia_estado_registra_intento_y_evento()
    {
        await _handler.Handle(
            Entrega(Guid.NewGuid()),
            CancellationToken.None);
        Assert.Single(_hoja.Paradas);
        Assert.False(_hoja.Paradas.Single().Activa);
        Assert.NotNull(_hoja.Paradas.Single().LiberadaEn);

        Assert.Equal(EstadoEnvio.Entregado, _envio.Estado);
        Assert.Equal(1, _envio.IntentosRealizados);
        Assert.Single(_envio.Intentos);
        Assert.True(_envio.Intentos.Single().Entregado);

        Assert.Single(_eventos.Lista);
        Assert.Single(_publicador.Publicados);
        Assert.Equal(1, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Intento_fallido_cambia_estado_y_registra_motivo()
    {
        await _handler.Handle(
            Fallo(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(EstadoEnvio.NoEntregado, _envio.Estado);
        Assert.Equal(1, _envio.IntentosRealizados);
        Assert.Single(_envio.Intentos);
        Assert.False(_envio.Intentos.Single().Entregado);
        Assert.Equal(_motivo.Id, _envio.UltimoMotivoNoEntregaId);

        Assert.Single(_eventos.Lista);
        Assert.Single(_publicador.Publicados);
        Assert.Equal(1, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Mismo_evento_dos_veces_no_duplica_intentos()
    {
        var id = Guid.NewGuid();

        await _handler.Handle(
            Entrega(id),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictoException>(() =>
            _handler.Handle(
                Entrega(id),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.Entregado, _envio.Estado);
        Assert.Equal(1, _envio.IntentosRealizados);
        Assert.Single(_eventos.Lista);
        Assert.Single(_publicador.Publicados);
        Assert.Equal(1, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Segunda_entrega_con_otro_evento_se_rechaza()
    {
        await _handler.Handle(
            Entrega(Guid.NewGuid(), "Ana"),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                Entrega(Guid.NewGuid(), "Otra persona"),
                CancellationToken.None));

        Assert.Contains("ya figura como entregado", ex.Message);
        Assert.Equal(EstadoEnvio.Entregado, _envio.Estado);
        Assert.Equal(1, _envio.IntentosRealizados);
        Assert.Single(_eventos.Lista);
    }

    [Fact]
    public async Task Envio_inexistente_da_no_encontrado()
    {
        var comando = Entrega(Guid.NewGuid()) with
        {
            EnvioId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Envio_que_no_esta_en_transito_se_rechaza()
    {
        // Un envío recién creado: queda Admitido, nunca se despachó.
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), _envio.OperadorId, Guid.NewGuid());
        var admitido = Envio.Crear(
            cuenta,
            "PED-300",
            Modalidad.Estandar,
            new Destinatario("Luis Gómez", null),
            new Direccion("18 de Julio", "512", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10)],
            Alta);
        _envios.Lista.Add(admitido);

        // El repartidor intenta entregarlo (mismos datos que una entrega válida, pero de este envío).
        var comando = Entrega(Guid.NewGuid()) with { EnvioId = admitido.Id };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        // Gana el servidor: se rechaza con el motivo, el estado no cambia y no se guarda nada.
        Assert.Contains("debe estar en tránsito", ex.Message);
        Assert.Equal(EstadoEnvio.Admitido, admitido.Estado);
        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Entrega_sin_coordenadas_se_rechaza()
    {
        var comando = Entrega(Guid.NewGuid()) with
        {
            Latitud = null,
            Longitud = null
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnTransito, _envio.Estado);
        Assert.Equal(0, _envio.IntentosRealizados);
        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Entrega_sin_firma_ni_foto_se_rechaza_si_reglas_lo_exigen()
    {
        var comando = Entrega(Guid.NewGuid()) with
        {
            ReferenciaFirma = null,
            ReferenciaFotografia = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnTransito, _envio.Estado);
        Assert.Equal(0, _envio.IntentosRealizados);
        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Intento_fallido_sin_evidencia_se_rechaza()
    {
        var comando = Fallo(Guid.NewGuid()) with
        {
            EvidenciaFallo = null
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnTransito, _envio.Estado);
        Assert.Equal(0, _envio.IntentosRealizados);
        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidadDeTrabajo.Guardados);
    }

    [Fact]
    public async Task Intento_fallido_con_motivo_ajeno_se_rechaza()
    {
        var comando = Fallo(Guid.NewGuid()) with
        {
            Motivo = "Motivo inexistente"
        };

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnTransito, _envio.Estado);
        Assert.Equal(0, _envio.IntentosRealizados);
        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidadDeTrabajo.Guardados);
    }

    // Dobles de prueba

    private sealed class HojasEnMemoria : IHojaDeRutaRepository
{
    public List<HojaDeRuta> Lista { get; } = [];

        public Task AgregarAsync(
            HojaDeRuta hoja,
            CancellationToken ct)
        {
            Lista.Add(hoja);
            return Task.CompletedTask;
        }

        public Task<HojaDeRuta?> ObtenerAsync(
            Guid operadorId,
            Guid hojaId,
            CancellationToken ct)
        {
            return Task.FromResult(
                Lista.FirstOrDefault(h =>
                    h.Id == hojaId &&
                    h.OperadorId == operadorId));
        }

        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
            Guid operadorId,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<HojaDeRuta>>(
                Lista.Where(h => h.OperadorId == operadorId).ToList());
        }

        public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
            Guid operadorId,
            Guid envioId,
            CancellationToken ct)
        {
            return Task.FromResult(
                Lista.SingleOrDefault(h =>
                    h.OperadorId == operadorId &&
                    h.Paradas.Any(p =>
                        p.EnvioId == envioId &&
                        p.Activa)));
        }
    }
    private sealed class EnviosEnMemoria : IEnvioRepository
    {
        public List<Envio> Lista { get; } = [];

        public Task AgregarAsync(Envio envio, CancellationToken ct)
        {
            Lista.Add(envio);
            return Task.CompletedTask;
        }

        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(e => e.Id == id));

        public Task<bool> ExisteReferenciaAsync(
            Guid cuentaComercialId,
            string referenciaExterna,
            CancellationToken ct) =>
            Task.FromResult(Lista.Any(e =>
                e.CuentaComercialId == cuentaComercialId &&
                e.ReferenciaExterna == referenciaExterna));

        public Task<IReadOnlyList<Envio>> ListarAsync(
            Guid operadorId,
            Guid? cuentaComercialId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(
                Lista.Where(e =>
                    e.OperadorId == operadorId &&
                    (!cuentaComercialId.HasValue ||
                    e.CuentaComercialId == cuentaComercialId.Value))
                .ToList());
    
        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(
            Guid operadorId,
            CancellationToken ct)
        {
            throw new NotSupportedException(
                "Esta prueba no utiliza la consulta de planificación.");
        }
    }

    private sealed class EventosEnMemoria : IEventoMovilRepository
    {
        public List<EventoMovil> Lista { get; } = [];

        public Task<bool> ExisteAsync(
            Guid idEvento,
            CancellationToken ct) =>
            Task.FromResult(Lista.Any(e => e.Id == idEvento));

        public Task<EventoMovil?> ObtenerEntregaAsync(
            Guid envioId,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(e =>
                e.EnvioId == envioId &&
                e.Tipo == TipoEventoMovil.Entregado));

        public Task AgregarAsync(EventoMovil evento, CancellationToken ct)
        {
            Lista.Add(evento);
            return Task.CompletedTask;
        }
    }

    private sealed class ReglasEnMemoria : IVersionReglasOperativasRepository
    {
        public List<VersionReglasOperativas> Lista { get; } = [];

        public Task<VersionReglasOperativas?> ObtenerVigenteAsync(
            Guid operadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(r =>
                r.OperadorId == operadorId &&
                r.EstaVigente(fecha)));

        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
            Guid operadorId,
            Guid versionId,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(r =>
                r.OperadorId == operadorId &&
                r.Id == versionId));
    }

    private sealed class MotivosEnMemoria : IMotivoNoEntregaRepository
    {
        public List<MotivoNoEntrega> Lista { get; } = [];

        public Task<MotivoNoEntrega?> ObtenerPorCodigoAsync(
            Guid operadorId,
            Guid versionReglasOperativasId,
            string codigo,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(m =>
                m.OperadorId == operadorId &&
                m.VersionReglasOperativasId == versionReglasOperativasId &&
                m.Codigo == codigo));

        public Task<MotivoNoEntrega?> ObtenerPorDescripcionAsync(
            Guid operadorId,
            Guid versionReglasOperativasId,
            string descripcion,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(m =>
                m.OperadorId == operadorId &&
                m.VersionReglasOperativasId == versionReglasOperativasId &&
                m.Descripcion == descripcion.Trim()));
    }

    private sealed class PublicadorEnMemoria : IPublicadorDeEventos
    {
        public List<object> Publicados { get; } = [];

        public Task PublicarAsync<T>(
            T evento,
            CancellationToken ct) where T : class
        {
            Publicados.Add(evento);
            return Task.CompletedTask;
        }
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

    private sealed class CacheEnMemoria : ICacheService
{
    private readonly Dictionary<string, object> _datos = new();

    public Task<T?> ObtenerAsync<T>(
        string clave,
        CancellationToken ct = default)
    {
        if (_datos.TryGetValue(clave, out var valor) && valor is T dato)
            return Task.FromResult<T?>(dato);

        return Task.FromResult<T?>(default);
    }

    public Task GuardarAsync<T>(
        string clave,
        T valor,
        TimeSpan duracion,
        CancellationToken ct = default)
    {
        _datos[clave] = valor!;
        return Task.CompletedTask;
    }

    public Task EliminarAsync(
        string clave,
        CancellationToken ct = default)
    {
        _datos.Remove(clave);
        return Task.CompletedTask;
    }
}
}
