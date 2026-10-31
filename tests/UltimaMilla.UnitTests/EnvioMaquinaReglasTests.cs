
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

public class EnvioMaquinaReglasTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static (Envio envio, VersionReglasOperativas reglas) Preparar(
        int maximoIntentos = 3,
        int horasEntreIntentos = 24,
        bool requierePrueba = true)
    {
        var operadorId = Guid.NewGuid();

        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            operadorId,
            Guid.NewGuid());

        var envio = Envio.Crear(
            cuenta,
            "PED-TEST",
            Modalidad.Estandar,
            new Destinatario("Ana Pérez", "099123456"),
            new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(2.5m, 20, 30, 40)],
            Ahora);

        var reglas = VersionReglasOperativas.Crear(
            operadorId,
            maximoIntentos,
            horasEntreIntentos,
            requierePrueba,
            7,
            Ahora.AddDays(-1));

        envio.AsignarReglasOperativas(reglas.Id);

        return (envio, reglas);
    }

    private static void Aplicar(
        Envio envio,
        VersionReglasOperativas reglas,
        DisparadorEnvio disparador,
        DateTimeOffset? fecha = null,
        PruebaEntrega? prueba = null,
        MotivoNoEntrega? motivo = null,
        string? evidencia = null,
        DateTimeOffset? fechaReprogramada = null,
        string? franja = null,
        string? justificacion = null)
    {
        envio.Aplicar(
            disparador,
            reglas,
            fecha ?? Ahora,
            "PruebaUnitaria",
            "OperarioTest",
            pruebaEntrega: prueba,
            motivoNoEntrega: motivo,
            evidenciaFallo: evidencia,
            fechaReprogramada: fechaReprogramada,
            franjaReprogramada: franja,
            justificacion: justificacion);
    }

    private static void PonerEnTransito(
        Envio envio,
        VersionReglasOperativas reglas)
    {
        Aplicar(envio, reglas, DisparadorEnvio.RecibirEnDeposito);
        Aplicar(envio, reglas, DisparadorEnvio.AsignarARuta);
        Aplicar(envio, reglas, DisparadorEnvio.Despachar);
    }

    private static MotivoNoEntrega CrearMotivo(
        Envio envio,
        VersionReglasOperativas reglas,
        bool requiereDevolucion = false) =>
        MotivoNoEntrega.Crear(
            envio.OperadorId,
            reglas.Id,
            "AUSENTE",
            "Destinatario ausente",
            requiereDevolucion);

    [Fact]
    public void Recibir_en_deposito_cambia_estado_y_registra_evento()
    {
        var (envio, reglas) = Preparar();

        Aplicar(envio, reglas, DisparadorEnvio.RecibirEnDeposito);

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);

        var evento = Assert.Single(envio.Eventos);
        Assert.Equal(EstadoEnvio.Admitido, evento.EstadoAnterior);
        Assert.Equal(EstadoEnvio.EnDeposito, evento.EstadoNuevo);
        Assert.Equal(DisparadorEnvio.RecibirEnDeposito, evento.Disparador);
        Assert.Equal("OperarioTest", evento.Responsable);
        Assert.Equal("PruebaUnitaria", evento.Origen);
    }

    [Fact]
    public void Recorrer_estados_genera_historial_en_orden()
    {
        var (envio, reglas) = Preparar();

        PonerEnTransito(envio, reglas);

        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
        Assert.Equal(3, envio.Eventos.Count);

        Assert.Equal(
            new[]
            {
                DisparadorEnvio.RecibirEnDeposito,
                DisparadorEnvio.AsignarARuta,
                DisparadorEnvio.Despachar
            },
            envio.Eventos.Select(e => e.Disparador));
    }

    [Fact]
    public void No_se_puede_entregar_desde_admitido()
    {
        var (envio, reglas) = Preparar();

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(envio, reglas, DisparadorEnvio.Entregar));

        Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
        Assert.Empty(envio.Eventos);
    }

    [Fact]
    public void Entrega_sin_prueba_es_rechazada()
    {
        var (envio, reglas) = Preparar();
        PonerEnTransito(envio, reglas);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(envio, reglas, DisparadorEnvio.Entregar));

        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
        Assert.Equal(3, envio.Eventos.Count);
        Assert.Empty(envio.Intentos);
    }

    [Fact]
    public void Entrega_sin_firma_ni_fotografia_es_rechazada_si_son_obligatorias()
    {
        var (envio, reglas) = Preparar(requierePrueba: true);
        PonerEnTransito(envio, reglas);

        var prueba = PruebaEntrega.Crear(
            "Ana Pérez",
            "12345678",
            null,
            null,
            Ahora,
            -34.9m,
            -56.2m);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(envio, reglas, DisparadorEnvio.Entregar, prueba: prueba));

        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
        Assert.Empty(envio.Intentos);
    }

    [Fact]
    public void Entrega_con_firma_cambia_estado_y_registra_intento()
    {
        var (envio, reglas) = Preparar();
        PonerEnTransito(envio, reglas);

        var prueba = PruebaEntrega.Crear(
            "Ana Pérez",
            "12345678",
            "firma-001",
            null,
            Ahora,
            -34.9m,
            -56.2m);

        Aplicar(envio, reglas, DisparadorEnvio.Entregar, prueba: prueba);

        Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
        Assert.Equal(1, envio.IntentosRealizados);
        Assert.Single(envio.Intentos);
        Assert.Equal(4, envio.Eventos.Count);
        Assert.Equal(DisparadorEnvio.Entregar, envio.Eventos.Last().Disparador);
    }

    [Fact]
    public void Estado_entregado_no_permite_nuevas_transiciones()
    {
        var (envio, reglas) = Preparar();
        PonerEnTransito(envio, reglas);

        var prueba = PruebaEntrega.Crear(
            "Ana Pérez",
            null,
            "firma-001",
            null,
            Ahora,
            -34.9m,
            -56.2m);

        Aplicar(envio, reglas, DisparadorEnvio.Entregar, prueba: prueba);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(envio, reglas, DisparadorEnvio.RecibirEnDeposito));

        Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
    }

    [Fact]
    public void Intento_fallido_registra_motivo_y_aumenta_contador()
    {
        var (envio, reglas) = Preparar();
        PonerEnTransito(envio, reglas);
        var motivo = CrearMotivo(envio, reglas);

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.RegistrarIntentoFallido,
            motivo: motivo,
            evidencia: "foto-intento-001");

        Assert.Equal(EstadoEnvio.NoEntregado, envio.Estado);
        Assert.Equal(1, envio.IntentosRealizados);
        Assert.Single(envio.Intentos);
        Assert.Equal(motivo.Id, envio.UltimoMotivoNoEntregaId);
    }

    [Fact]
    public void Intento_fallido_sin_motivo_valido_es_rechazado()
    {
        var (envio, reglas) = Preparar();
        PonerEnTransito(envio, reglas);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(
                envio,
                reglas,
                DisparadorEnvio.RegistrarIntentoFallido,
                evidencia: "foto-intento-001"));

        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
        Assert.Equal(0, envio.IntentosRealizados);
    }

    [Fact]
    public void Motivo_de_otro_operador_es_rechazado()
    {
        var (envio, reglas) = Preparar();
        PonerEnTransito(envio, reglas);

        var motivoAjeno = MotivoNoEntrega.Crear(
            Guid.NewGuid(),
            reglas.Id,
            "AUSENTE",
            "Destinatario ausente",
            false);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(
                envio,
                reglas,
                DisparadorEnvio.RegistrarIntentoFallido,
                motivo: motivoAjeno,
                evidencia: "foto-intento-001"));

        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
    }

    [Fact]
    public void Reprogramar_sin_intento_fallido_es_rechazado()
    {
        var (envio, reglas) = Preparar();

        Aplicar(envio, reglas, DisparadorEnvio.RecibirEnDeposito);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(
                envio,
                reglas,
                DisparadorEnvio.Reprogramar,
                fechaReprogramada: Ahora.AddDays(2),
                franja: "14:00-18:00"));

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
    }

    [Fact]
    public void Reprogramar_antes_del_intervalo_minimo_es_rechazado()
    {
        var (envio, reglas) = Preparar(horasEntreIntentos: 24);
        PonerEnTransito(envio, reglas);
        var motivo = CrearMotivo(envio, reglas);

        Aplicar(
            envio, reglas, DisparadorEnvio.RegistrarIntentoFallido,
            motivo: motivo, evidencia: "foto-001");

        Aplicar(envio, reglas, DisparadorEnvio.RetornarADeposito);

        // Se aprueba la reprogramación sin fecha.
        Aplicar(envio, reglas, DisparadorEnvio.Reprogramar);

        Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);

        // El último fallo fue en Ahora y el operador exige 24 horas.
        // Intentar una nueva entrega 12 horas después debe rechazarse.
        var inicioNuevaEntrega = Ahora.AddHours(12);

        Assert.Throws<InvalidOperationException>(() =>
            envio.EstablecerCompromisoEntrega(
                DateOnly.FromDateTime(inicioNuevaEntrega.Date),
                new TimeOnly(0, 0),
                new TimeOnly(4, 0),
                reglas,
                inicioNuevaEntrega));

        Assert.Null(envio.FechaReprogramada);
        Assert.Null(envio.FranjaReprogramada);
    }

    [Fact]
    public void Reprogramar_respetando_intervalo_minimo_es_valido()
    {
        var (envio, reglas) = Preparar(horasEntreIntentos: 24);
        PonerEnTransito(envio, reglas);
        var motivo = CrearMotivo(envio, reglas);

        Aplicar(
            envio, reglas, DisparadorEnvio.RegistrarIntentoFallido,
            motivo: motivo, evidencia: "foto-001");

        Aplicar(envio, reglas, DisparadorEnvio.RetornarADeposito);

        // Primero se aprueba sin fecha.
        Aplicar(envio, reglas, DisparadorEnvio.Reprogramar);

        Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
        Assert.Null(envio.FechaReprogramada);

        // La nueva entrega será 25 horas después del fallo.
        var inicioNuevaEntrega = Ahora.AddHours(25);

        envio.EstablecerCompromisoEntrega(
            DateOnly.FromDateTime(inicioNuevaEntrega.Date),
            TimeOnly.FromDateTime(inicioNuevaEntrega.DateTime),
            TimeOnly.FromDateTime(inicioNuevaEntrega.AddHours(2).DateTime),
            reglas,
            inicioNuevaEntrega);

        Assert.Equal(inicioNuevaEntrega.ToUniversalTime(), envio.FechaReprogramada);
        Assert.Equal(
            DateOnly.FromDateTime(inicioNuevaEntrega.Date),
            envio.FechaEntregaComprometida);

        Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);

        // Después de planificar, puede asignarse a una ruta.
        Aplicar(envio, reglas, DisparadorEnvio.AsignarARuta);

        Assert.Equal(EstadoEnvio.AsignadoARuta, envio.Estado);
    }

    [Fact]
    public void Motivo_que_exige_devolucion_permite_retorno_pero_no_reprogramacion()
    {
        var (envio, reglas) = Preparar();
        PonerEnTransito(envio, reglas);
        var motivo = CrearMotivo(envio, reglas, requiereDevolucion: true);

        Aplicar(
            envio, reglas, DisparadorEnvio.RegistrarIntentoFallido,
            motivo: motivo, evidencia: "foto-001");

        Aplicar(envio, reglas, DisparadorEnvio.RetornarADeposito);

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(
                envio,
                reglas,
                DisparadorEnvio.Reprogramar,
                fechaReprogramada: Ahora.AddHours(48),
                franja: "14:00-18:00"));

        Aplicar(envio, reglas, DisparadorEnvio.IniciarDevolucion);

        Assert.Equal(EstadoEnvio.EnDevolucion, envio.Estado);
    }

    [Fact]
    public void No_se_puede_iniciar_devolucion_sin_cumplir_condiciones()
    {
        var (envio, reglas) = Preparar(maximoIntentos: 3);
        PonerEnTransito(envio, reglas);
        var motivo = CrearMotivo(envio, reglas);

        Aplicar(
            envio, reglas, DisparadorEnvio.RegistrarIntentoFallido,
            motivo: motivo, evidencia: "foto-001");
        Aplicar(envio, reglas, DisparadorEnvio.RetornarADeposito);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(envio, reglas, DisparadorEnvio.IniciarDevolucion));

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
    }

    [Fact]
    public void Al_alcanzar_maximo_de_intentos_corresponde_devolucion()
    {
        var (envio, reglas) = Preparar(maximoIntentos: 1);
        PonerEnTransito(envio, reglas);
        var motivo = CrearMotivo(envio, reglas);

        Aplicar(
            envio, reglas, DisparadorEnvio.RegistrarIntentoFallido,
            motivo: motivo, evidencia: "foto-001");

        Aplicar(envio, reglas, DisparadorEnvio.RetornarADeposito);

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(
                envio,
                reglas,
                DisparadorEnvio.Reprogramar,
                fechaReprogramada: Ahora.AddHours(48),
                franja: "14:00-18:00"));

        Aplicar(envio, reglas, DisparadorEnvio.IniciarDevolucion);

        Assert.Equal(EstadoEnvio.EnDevolucion, envio.Estado);
        Assert.Equal(1, envio.IntentosRealizados);
    }

    [Fact]
    public void Extravío_exige_justificacion()
    {
        var (envio, reglas) = Preparar();
        Aplicar(envio, reglas, DisparadorEnvio.RecibirEnDeposito);

        Assert.Throws<ArgumentException>(() =>
            Aplicar(envio, reglas, DisparadorEnvio.DeclararExtraviado));

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.DeclararExtraviado,
            justificacion: "No se encontró el bulto en el depósito.");

        Assert.Equal(EstadoEnvio.Extraviado, envio.Estado);
    }

    [Fact]
    public void Reglas_de_otro_envio_son_rechazadas()
    {
        var (envio, _) = Preparar();
        var (_, reglasAjenas) = Preparar();

        Assert.Throws<InvalidOperationException>(() =>
            Aplicar(envio, reglasAjenas, DisparadorEnvio.RecibirEnDeposito));

        Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
        Assert.Empty(envio.Eventos);
    }

    [Fact]
    public void QuitarDeRuta_Reprogramado_RestauraEstadoYConservaIntentos()
    {
        var (envio, reglas) = Preparar(horasEntreIntentos: 24);

        PonerEnTransito(envio, reglas);

        var motivo = CrearMotivo(envio, reglas);

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.RegistrarIntentoFallido,
            motivo: motivo,
            evidencia: "foto-intento-001");

        Aplicar(envio, reglas, DisparadorEnvio.RetornarADeposito);
        Aplicar(envio, reglas, DisparadorEnvio.Reprogramar);

        var inicio = Ahora.AddDays(2);

        envio.EstablecerCompromisoEntrega(
            DateOnly.FromDateTime(inicio.Date),
            new TimeOnly(10, 0),
            new TimeOnly(12, 0),
            reglas,
            inicio);

        Aplicar(envio, reglas, DisparadorEnvio.AsignarARuta);

        Assert.Equal(EstadoEnvio.AsignadoARuta, envio.Estado);
        Assert.Equal(1, envio.IntentosRealizados);

        Aplicar(envio, reglas, DisparadorEnvio.QuitarDeRuta);

        Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
        Assert.Equal(1, envio.IntentosRealizados);
        Assert.Single(envio.Intentos);

        Assert.Null(envio.FechaReprogramada);
        Assert.Null(envio.FranjaReprogramada);
        Assert.Null(envio.FechaEntregaComprometida);
        Assert.Null(envio.HoraEntregaDesde);
        Assert.Null(envio.HoraEntregaHasta);

        var ultimoEvento = envio.Eventos.Last();

        Assert.Equal(DisparadorEnvio.QuitarDeRuta, ultimoEvento.Disparador);
        Assert.Equal(EstadoEnvio.AsignadoARuta, ultimoEvento.EstadoAnterior);
        Assert.Equal(EstadoEnvio.Reprogramado, ultimoEvento.EstadoNuevo);
    }

    [Fact]
    public void DeclararExtravio_GuardaJustificacionEnHistorial()
    {
        var (envio, reglas) = Preparar();

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.RecibirEnDeposito);

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.DeclararExtraviado,
            justificacion: "  Bulto perdido en depósito  ");

        Assert.Equal(EstadoEnvio.Extraviado, envio.Estado);

        var evento = envio.Eventos.Last();

        Assert.Equal(
            DisparadorEnvio.DeclararExtraviado,
            evento.Disparador);

        Assert.Equal(
            EstadoEnvio.EnDeposito,
            evento.EstadoAnterior);

        Assert.Equal(
            EstadoEnvio.Extraviado,
            evento.EstadoNuevo);

        Assert.Equal(
            "Bulto perdido en depósito",
            evento.Justificacion);

        Assert.Equal("OperarioTest", evento.Responsable);
    }

    [Fact]
    public void Reaparecer_RestauraEnDepositoYConservaHistorial()
    {
        var (envio, reglas) = Preparar();

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.RecibirEnDeposito);

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.DeclararExtraviado,
            justificacion: "Bulto extraviado");

        Aplicar(
            envio,
            reglas,
            DisparadorEnvio.Reaparecer);

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        Assert.Equal(3, envio.Eventos.Count);

        var ultimoEvento = envio.Eventos.Last();

        Assert.Equal(
            DisparadorEnvio.Reaparecer,
            ultimoEvento.Disparador);

        Assert.Equal(
            EstadoEnvio.Extraviado,
            ultimoEvento.EstadoAnterior);

        Assert.Equal(
            EstadoEnvio.EnDeposito,
            ultimoEvento.EstadoNuevo);

        Assert.Contains(
            envio.Eventos,
            e => e.Disparador == DisparadorEnvio.DeclararExtraviado &&
                e.Justificacion == "Bulto extraviado");
    }
}
