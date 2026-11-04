
using System.Reflection;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.Commands.Reprogramacion;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

// PRUEBAS DE LA ENTIDAD
public class SolicitudReprogramacionTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nueva_solicitud_queda_pendiente()
    {
        var operadorId = Guid.NewGuid();
        var envioId = Guid.NewGuid();

        var solicitud = new SolicitudReprogramacion(
            operadorId, envioId, Ahora);

        Assert.NotEqual(Guid.Empty, solicitud.Id);
        Assert.Equal(operadorId, solicitud.OperadorId);
        Assert.Equal(envioId, solicitud.EnvioId);
        Assert.Equal(Ahora, solicitud.SolicitadaEn);
        Assert.Equal(
            EstadoSolicitudReprogramacion.Pendiente,
            solicitud.Estado);
        Assert.Null(solicitud.AtendidaEn);
    }

    [Fact]
    public void Marcar_atendida_actualiza_estado_y_fecha()
    {
        var solicitud = CrearSolicitud();

        solicitud.MarcarAtendida(Ahora.AddHours(2));

        Assert.Equal(
            EstadoSolicitudReprogramacion.Atendida,
            solicitud.Estado);
        Assert.Equal(Ahora.AddHours(2), solicitud.AtendidaEn);
    }

    [Fact]
    public void Rechazar_actualiza_estado_y_fecha()
    {
        var solicitud = CrearSolicitud();

        solicitud.Rechazar(Ahora.AddHours(1));

        Assert.Equal(
            EstadoSolicitudReprogramacion.Rechazada,
            solicitud.Estado);
        Assert.Equal(Ahora.AddHours(1), solicitud.AtendidaEn);
    }

    [Fact]
    public void No_se_puede_atender_dos_veces()
    {
        var solicitud = CrearSolicitud();
        solicitud.MarcarAtendida(Ahora);

        Assert.Throws<InvalidOperationException>(
            () => solicitud.MarcarAtendida(Ahora.AddHours(1)));
    }

    [Fact]
    public void No_se_puede_rechazar_una_solicitud_atendida()
    {
        var solicitud = CrearSolicitud();
        solicitud.MarcarAtendida(Ahora);

        Assert.Throws<InvalidOperationException>(
            () => solicitud.Rechazar(Ahora.AddHours(1)));
    }

    [Fact]
    public void No_se_puede_crear_sin_operador()
    {
        Assert.Throws<ArgumentException>(() =>
            new SolicitudReprogramacion(
                Guid.Empty, Guid.NewGuid(), Ahora));
    }

    [Fact]
    public void No_se_puede_crear_sin_envio()
    {
        Assert.Throws<ArgumentException>(() =>
            new SolicitudReprogramacion(
                Guid.NewGuid(), Guid.Empty, Ahora));
    }

    private static SolicitudReprogramacion CrearSolicitud() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Ahora);
}

// PRUEBAS DEL HANDLER DE SOLICITUD
public class SolicitarReprogramacionHandlerTests
{
    [Fact]
    public async Task Solicitud_valida_queda_pendiente_sin_cambiar_envio()
    {
        var escenario = new EscenarioReprogramacion();
        escenario.RegistrarIntentoFallido();

        var handler = escenario.CrearSolicitarHandler();

        var id = await handler.Handle(
            new SolicitarReprogramacionCommand(
                escenario.Envio.Id),
            CancellationToken.None);

        var solicitud = Assert.Single(escenario.Solicitudes);

        Assert.Equal(id, solicitud.Id);
        Assert.Equal(escenario.Envio.Id, solicitud.EnvioId);
        Assert.Equal(
            EstadoSolicitudReprogramacion.Pendiente,
            solicitud.Estado);
        Assert.Equal(EstadoEnvio.EnDeposito, escenario.Envio.Estado);
        Assert.Equal(1, escenario.Guardados);
    }

    [Fact]
    public async Task No_permite_solicitar_sin_intento_fallido()
    {
        var escenario = new EscenarioReprogramacion();
        escenario.RecibirEnDeposito();

        var handler = escenario.CrearSolicitarHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new SolicitarReprogramacionCommand(
                    escenario.Envio.Id),
                CancellationToken.None));

        Assert.Empty(escenario.Solicitudes);
        Assert.Equal(0, escenario.Guardados);
    }

    [Fact]
    public async Task No_permite_solicitudes_pendientes_duplicadas()
    {
        var escenario = new EscenarioReprogramacion();
        escenario.RegistrarIntentoFallido();
        escenario.CrearSolicitudPendiente();

        var handler = escenario.CrearSolicitarHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new SolicitarReprogramacionCommand(
                    escenario.Envio.Id),
                CancellationToken.None));

        Assert.Single(escenario.Solicitudes);
        Assert.Equal(0, escenario.Guardados);
    }

    [Fact]
    public async Task No_permite_solicitar_si_agoto_los_intentos()
    {
        var escenario = new EscenarioReprogramacion(
            maximoIntentos: 1);

        escenario.RegistrarIntentoFallido();

        var handler = escenario.CrearSolicitarHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new SolicitarReprogramacionCommand(
                    escenario.Envio.Id),
                CancellationToken.None));

        Assert.Empty(escenario.Solicitudes);
        Assert.Equal(0, escenario.Guardados);
    }
}

// PRUEBAS DEL HANDLER DE CONFIRMACION
public class ConfirmarReprogramacionHandlerTests
{
    [Fact]
    public async Task Confirmacion_valida_reprograma_sin_fecha_y_atiende_solicitud()
    {
        var escenario = new EscenarioReprogramacion();
        escenario.RegistrarIntentoFallido();

        var solicitud = escenario.CrearSolicitudPendiente();
        var handler = escenario.CrearConfirmarHandler();

        await handler.Handle(
            new ConfirmarReprogramacionCommand(
                solicitud.Id,
                "Despachador de prueba"),
            CancellationToken.None);

        Assert.Equal(
            EstadoEnvio.Reprogramado,
            escenario.Envio.Estado);

        Assert.Null(escenario.Envio.FechaReprogramada);
        Assert.Null(escenario.Envio.FranjaReprogramada);

        Assert.Equal(
            EstadoSolicitudReprogramacion.Atendida,
            solicitud.Estado);

        Assert.Equal(escenario.Ahora, solicitud.AtendidaEn);
        Assert.Equal(1, escenario.Guardados);

        Assert.Contains(
            escenario.Envio.Eventos,
            e => e.Disparador == DisparadorEnvio.Reprogramar);
    }

    [Fact]
    public async Task No_permite_confirmar_solicitud_ya_atendida()
    {
        var escenario = new EscenarioReprogramacion();
        escenario.RegistrarIntentoFallido();

        var solicitud = escenario.CrearSolicitudPendiente();
        solicitud.MarcarAtendida(escenario.Ahora);

        var handler = escenario.CrearConfirmarHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new ConfirmarReprogramacionCommand(
                    solicitud.Id,
                    "Despachador"),
                CancellationToken.None));

        Assert.Equal(
            EstadoEnvio.EnDeposito,
            escenario.Envio.Estado);

        Assert.Equal(0, escenario.Guardados);
    }

    [Fact]
    public async Task No_permite_confirmar_si_envio_no_esta_en_deposito()
    {
        var escenario = new EscenarioReprogramacion();
        escenario.PonerEnTransito();

        var solicitud = escenario.CrearSolicitudPendiente();
        var handler = escenario.CrearConfirmarHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new ConfirmarReprogramacionCommand(
                    solicitud.Id,
                    "Despachador"),
                CancellationToken.None));

        Assert.Equal(
            EstadoEnvio.EnTransito,
            escenario.Envio.Estado);

        Assert.Equal(
            EstadoSolicitudReprogramacion.Pendiente,
            solicitud.Estado);

        Assert.Equal(0, escenario.Guardados);
    }
}

// INFRAESTRUCTURA DE PRUEBAS EN MEMORIA.
// No utiliza PostgreSQL ni modifica el sistema real.
internal sealed class EscenarioReprogramacion
{
    public DateTimeOffset Ahora { get; } =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    public Envio Envio { get; }
    public VersionReglasOperativas Reglas { get; }

    public List<SolicitudReprogramacion> Solicitudes { get; } = [];

    public int Guardados { get; private set; }

    private readonly IEnvioRepository _envios;
    private readonly ISolicitudReprogramacionRepository _solicitudes;
    private readonly IVersionReglasOperativasRepository _reglas;
    private readonly IUnitOfWork _unidadDeTrabajo;
    private readonly TimeProvider _reloj;

    public EscenarioReprogramacion(int maximoIntentos = 3)
    {
        var operadorId = Guid.NewGuid();

        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            operadorId,
            Guid.NewGuid());

        Envio = Envio.Crear(
            cuenta,
            "PED-REPROGRAMACION",
            Modalidad.Estandar,
            new Destinatario("Ana Perez", "099123456"),
            new Direccion(
                "Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(2.5m, 20, 30, 40)],
            Ahora);

        Reglas = VersionReglasOperativas.Crear(
            operadorId,
            maximoIntentos,
            24,
            true,
            7,
            Ahora.AddDays(-1));

        Envio.AsignarReglasOperativas(Reglas.Id);

        _reloj = new RelojFijo(Ahora);

        _envios = CrearSimulacion<IEnvioRepository>(
            (metodo, argumentos) => metodo.Name switch
            {
                "ObtenerAsync" => Task.FromResult<Envio?>(
                    (Guid)argumentos![0]! == Envio.Id
                        ? Envio
                        : null),

                _ => throw new NotSupportedException(metodo.Name)
            });

        _solicitudes = CrearSimulacion<
            ISolicitudReprogramacionRepository>(
            (metodo, argumentos) => metodo.Name switch
            {
                "AgregarAsync" => AgregarSolicitud(
                    (SolicitudReprogramacion)argumentos![0]!),

                "ObtenerAsync" =>
                    Task.FromResult<SolicitudReprogramacion?>(
                        Solicitudes.FirstOrDefault(
                            s => s.Id == (Guid)argumentos![0]!)),

                "ExistePendienteAsync" => Task.FromResult(
                    Solicitudes.Any(s =>
                        s.EnvioId == (Guid)argumentos![0]! &&
                        s.Estado ==
                        EstadoSolicitudReprogramacion.Pendiente)),

                _ => throw new NotSupportedException(metodo.Name)
            });

        _reglas = CrearSimulacion<
            IVersionReglasOperativasRepository>(
            (metodo, argumentos) => metodo.Name switch
            {
                "ObtenerPorIdAsync" =>
                    Task.FromResult<VersionReglasOperativas?>(
                        (Guid)argumentos![0]! == Envio.OperadorId &&
                        (Guid)argumentos[1]! == Reglas.Id
                            ? Reglas
                            : null),

                _ => throw new NotSupportedException(metodo.Name)
            });

        _unidadDeTrabajo = CrearSimulacion<IUnitOfWork>(
            (metodo, _) => metodo.Name switch
            {
                "GuardarCambiosAsync" => GuardarCambios(),

                _ => throw new NotSupportedException(metodo.Name)
            });
    }

    public SolicitarReprogramacionHandler CrearSolicitarHandler() =>
        new(
            _envios,
            _solicitudes,
            _reglas,
            _unidadDeTrabajo,
            _reloj);

    public ConfirmarReprogramacionHandler CrearConfirmarHandler() =>
        new(
            _solicitudes,
            _envios,
            _reglas,
            _unidadDeTrabajo,
            _reloj);

    public SolicitudReprogramacion CrearSolicitudPendiente()
    {
        var solicitud = new SolicitudReprogramacion(
            Envio.OperadorId,
            Envio.Id,
            Ahora);

        Solicitudes.Add(solicitud);
        return solicitud;
    }

    public void RecibirEnDeposito()
    {
        Aplicar(DisparadorEnvio.RecibirEnDeposito);
    }

    public void PonerEnTransito()
    {
        RecibirEnDeposito();
        Aplicar(DisparadorEnvio.AsignarARuta);
        Aplicar(DisparadorEnvio.Despachar);
    }

    public void RegistrarIntentoFallido()
    {
        PonerEnTransito();

        var motivo = MotivoNoEntrega.Crear(
            Envio.OperadorId,
            Reglas.Id,
            "AUSENTE",
            "Destinatario ausente",
            false);

        Envio.Aplicar(
            DisparadorEnvio.RegistrarIntentoFallido,
            Reglas,
            Ahora,
            "PruebaUnitaria",
            "Repartidor",
            motivoNoEntrega: motivo,
            evidenciaFallo: "Destinatario ausente");

        Aplicar(DisparadorEnvio.RetornarADeposito);
    }

    private void Aplicar(DisparadorEnvio disparador)
    {
        Envio.Aplicar(
            disparador,
            Reglas,
            Ahora,
            "PruebaUnitaria",
            "Operario");
    }

    private Task AgregarSolicitud(
        SolicitudReprogramacion solicitud)
    {
        Solicitudes.Add(solicitud);
        return Task.CompletedTask;
    }

    private Task<int> GuardarCambios()
    {
        Guardados++;
        return Task.FromResult(1);
    }

    private static T CrearSimulacion<T>(
        Func<MethodInfo, object?[]?, object?> ejecutar)
        where T : class
    {
        var simulacion = DispatchProxy.Create<T, RepositorioSimulado>();
        ((RepositorioSimulado)(object)simulacion).Ejecutar = ejecutar;
        return simulacion;
    }
}

public class RepositorioSimulado : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Ejecutar { get; set; }
        = (_, _) => throw new NotSupportedException();

    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        return Ejecutar(targetMethod, args);
    }
}

internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}
