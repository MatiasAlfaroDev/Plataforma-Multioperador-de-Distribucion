
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Domain.Retiros;
using Xunit;

namespace UltimaMilla.UnitTests;

public class HojaDeRutaTests
{
    
    private static HojaDeRuta CrearRuta(
        Guid operadorId,
        int maximoParadas = 5,
        decimal capacidadPeso = 100m,
        decimal capacidadVolumen = 10m)
    {
        var repartidor = Repartidor.Crear(
            operadorId,
            "Repartidor de prueba",
            "099123456");

        var vehiculo = Vehiculo.Crear(
            operadorId,
            "STP1234",
            capacidadPeso,
            capacidadVolumen);

        return HojaDeRuta.Crear(
            operadorId,
            repartidor,
            vehiculo,
            DateOnly.FromDateTime(DateTime.Today),
            maximoParadas);
    }


    private static Envio CrearEnvio(
        Guid operadorId,
        bool recibirEnDeposito = true,
        decimal peso = 2m,
        Action<VersionReglasOperativas>? capturarReglas = null)
    {
        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            operadorId,
            Guid.NewGuid());

        var ahora = DateTimeOffset.UtcNow;

        var envio = Envio.Crear(
            cuenta,
            $"TEST-{Guid.NewGuid():N}",
            Modalidad.Estandar,
            new Destinatario("Destinatario Test", "099123456"),
            new Direccion("Calle Test", "123", "San José", "San José"),
            [new NuevoBulto(peso, 20, 30, 40)],
            ahora);

        if (recibirEnDeposito)
        {
            var reglas = VersionReglasOperativas.Crear(
                operadorId,
                maximoIntentosEntrega: 3,
                horasMinimasEntreIntentos: 24,
                requierePruebaEntrega: true,
                diasMaximosParaDevolucion: 7,
                vigenciaDesde: ahora.AddDays(-1));

            envio.AsignarReglasOperativas(reglas.Id);
            capturarReglas?.Invoke(reglas);

            envio.Aplicar(
                DisparadorEnvio.RecibirEnDeposito,
                reglas,
                ahora,
                "Prueba",
                "OperarioTest");
        }

        return envio;
    }

    private static (
        Envio Envio,
        VersionReglasOperativas Reglas)
        CrearEnvioEnDevolucion(Guid operadorId)
    {
        var ahora = DateTimeOffset.UtcNow;

        VersionReglasOperativas? reglas = null;

        var envio = CrearEnvio(
            operadorId,
            capturarReglas: r => reglas = r);

        if (reglas is null)
            throw new InvalidOperationException(
                "No se crearon las reglas operativas.");

        envio.Aplicar(
            DisparadorEnvio.AsignarARuta,
            reglas,
            ahora,
            "Prueba",
            "Despachador");

        envio.Aplicar(
            DisparadorEnvio.Despachar,
            reglas,
            ahora,
            "Prueba",
            "Repartidor");

        var motivo = MotivoNoEntrega.Crear(
            operadorId,
            reglas.Id,
            "AUSENTE",
            "Destinatario ausente",
            requiereDevolucion: true);

        envio.Aplicar(
            DisparadorEnvio.RegistrarIntentoFallido,
            reglas,
            ahora,
            "Prueba",
            "Repartidor",
            motivoNoEntrega: motivo,
            evidenciaFallo: "foto-intento-fallido");

        envio.Aplicar(
            DisparadorEnvio.RetornarADeposito,
            reglas,
            ahora,
            "Prueba",
            "Operario");

        envio.Aplicar(
            DisparadorEnvio.IniciarDevolucion,
            reglas,
            ahora,
            "Prueba",
            "Operario");

        return (envio, reglas);
    }

    private static Retiro CrearRetiroEnDeposito(Guid operadorId)
    {
        var ahora = DateTimeOffset.UtcNow;

        var retiro = Retiro.Crear(
            operadorId,
            Guid.NewGuid(),
            "Devolución del cliente",
            new Direccion("Calle Origen", "100", "San José", "San José"),
            new Direccion("Calle Comercio", "200", "San José", "San José"),
            DateOnly.FromDateTime(ahora.Date),
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [BultoRetiro.Crear("RET-001", 5m, 20, 20, 20)],
            ahora);

        retiro.AsignarARuta(Guid.NewGuid());
        retiro.RegistrarRetiro(
            ahora,
            "foto-retiro",
            "firma-retiro");

        retiro.RecibirEnDeposito();

        return retiro;
    }

    [Fact]
    public void AgregarEnvioValido_CreaParada()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var envio = CrearEnvio(operadorId);

        ruta.AgregarEnvio(envio);

        var parada = Assert.Single(ruta.Paradas);

        Assert.Equal(envio.Id, parada.EnvioId);
        Assert.Equal(1, parada.Orden);
        Assert.Equal(envio.PesoTotalKg, ruta.PesoAsignadoKg);
        Assert.Equal(
            envio.VolumenTotalMetrosCubicos,
            ruta.VolumenAsignadoM3);
    }

    [Fact]
    public void AgregarEnvioAdmitido_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var envio = CrearEnvio(
            operadorId,
            recibirEnDeposito: false);

        Assert.Throws<InvalidOperationException>(
            () => ruta.AgregarEnvio(envio));

        Assert.Empty(ruta.Paradas);
    }

    [Fact]
    public void AgregarEnvioDeOtroOperador_DebeRechazarse()
    {
        var ruta = CrearRuta(Guid.NewGuid());
        var envio = CrearEnvio(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () => ruta.AgregarEnvio(envio));

        Assert.Empty(ruta.Paradas);
    }

    [Fact]
    public void AgregarMismoEnvioDosVeces_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var envio = CrearEnvio(operadorId);

        ruta.AgregarEnvio(envio);

        Assert.Throws<InvalidOperationException>(
            () => ruta.AgregarEnvio(envio));

        Assert.Single(ruta.Paradas);
    }

    [Fact]
    public void SuperarMaximoParadas_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId, maximoParadas: 1);

        ruta.AgregarEnvio(CrearEnvio(operadorId));

        Assert.Throws<InvalidOperationException>(
            () => ruta.AgregarEnvio(CrearEnvio(operadorId)));

        Assert.Single(ruta.Paradas);
    }

    [Fact]
    public void SuperarCapacidadPeso_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(
            operadorId,
            capacidadPeso: 1m);

        var envio = CrearEnvio(operadorId, peso: 2m);

        Assert.Throws<InvalidOperationException>(
            () => ruta.AgregarEnvio(envio));

        Assert.Empty(ruta.Paradas);
        Assert.Equal(0m, ruta.PesoAsignadoKg);
    }

    [Fact]
    public void SuperarCapacidadVolumen_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(
            operadorId,
            capacidadVolumen: 0.01m);

        var envio = CrearEnvio(operadorId);

        Assert.Throws<InvalidOperationException>(
            () => ruta.AgregarEnvio(envio));

        Assert.Empty(ruta.Paradas);
        Assert.Equal(0m, ruta.VolumenAsignadoM3);
    }

    [Fact]
    public void AgregarDosEnvios_RespetaOrdenDeParadas()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var primero = CrearEnvio(operadorId);
        var segundo = CrearEnvio(operadorId);

        ruta.AgregarEnvio(primero);
        ruta.AgregarEnvio(segundo);

        Assert.Equal(2, ruta.Paradas.Count);
        Assert.Equal(
            new[] { primero.Id, segundo.Id },
            ruta.Paradas
                .OrderBy(p => p.Orden)
                .Where(p => p.EnvioId.HasValue)
                .Select(p => p.EnvioId.Value));
    }
    
    [Fact]
    public void CrearRuta_ConRepartidorInactivo_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var repartidor = Repartidor.Crear(
            operadorId, "Repartidor", "099123456");
        var vehiculo = Vehiculo.Crear(
            operadorId, "STP1234", 100m, 10m);

        repartidor.Desactivar();

        Assert.Throws<InvalidOperationException>(() =>
            HojaDeRuta.Crear(
                operadorId, repartidor, vehiculo,
                DateOnly.FromDateTime(DateTime.Today), 5));
    }

    [Fact]
    public void CrearRuta_ConVehiculoInactivo_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var repartidor = Repartidor.Crear(
            operadorId, "Repartidor", "099123456");
        var vehiculo = Vehiculo.Crear(
            operadorId, "STP1234", 100m, 10m);

        vehiculo.Desactivar();

        Assert.Throws<InvalidOperationException>(() =>
            HojaDeRuta.Crear(
                operadorId, repartidor, vehiculo,
                DateOnly.FromDateTime(DateTime.Today), 5));
    }

    [Fact]
    public void CrearRuta_ConRepartidorDeOtroOperador_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var repartidor = Repartidor.Crear(
            Guid.NewGuid(), "Repartidor", "099123456");
        var vehiculo = Vehiculo.Crear(
            operadorId, "STP1234", 100m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            HojaDeRuta.Crear(
                operadorId, repartidor, vehiculo,
                DateOnly.FromDateTime(DateTime.Today), 5));
    }

    [Fact]
    public void CrearRuta_ConVehiculoDeOtroOperador_DebeRechazarse()
    {
        var operadorId = Guid.NewGuid();
        var repartidor = Repartidor.Crear(
            operadorId, "Repartidor", "099123456");
        var vehiculo = Vehiculo.Crear(
            Guid.NewGuid(), "STP1234", 100m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            HojaDeRuta.Crear(
                operadorId, repartidor, vehiculo,
                DateOnly.FromDateTime(DateTime.Today), 5));
    }

    [Fact]
    public void ReordenarParadas_ActualizaOrdenDeLasActivas()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);

        var primero = CrearEnvio(operadorId);
        var segundo = CrearEnvio(operadorId);

        ruta.AgregarEnvio(primero);
        ruta.AgregarEnvio(segundo);

        ruta.ActualizarOrdenParadas(
        [
            new HorarioParada(
                segundo.Id,
                new TimeOnly(10, 0),
                new TimeOnly(10, 0)),
            new HorarioParada(
                primero.Id,
                new TimeOnly(11, 0),
                new TimeOnly(11, 0))
        ]);

        Assert.Equal(
            segundo.Id,
            ruta.Paradas.Single(p => p.Orden == 1).EnvioId);

        Assert.Equal(
            primero.Id,
            ruta.Paradas.Single(p => p.Orden == 2).EnvioId);
    }

    [Fact]
    public void ReordenarParadas_RechazaSeleccionIncompleta()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);

        var primero = CrearEnvio(operadorId);
        var segundo = CrearEnvio(operadorId);

        ruta.AgregarEnvio(primero);
        ruta.AgregarEnvio(segundo);

        Assert.Throws<InvalidOperationException>(() =>
            ruta.ActualizarOrdenParadas(
            [
                new HorarioParada(
                    primero.Id,
                    new TimeOnly(10, 0),
                    new TimeOnly(10, 0))
            ]));

        Assert.Equal(1, ruta.Paradas.Single(p => p.EnvioId == primero.Id).Orden);
        Assert.Equal(2, ruta.Paradas.Single(p => p.EnvioId == segundo.Id).Orden);
    }

    [Fact]
    public void HojaDespachada_NoPermiteAgregarNiQuitar()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);

        var primero = CrearEnvio(operadorId);
        var segundo = CrearEnvio(operadorId);

        ruta.AgregarEnvio(primero);
        ruta.MarcarDespachada(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            ruta.AgregarEnvio(segundo));

        Assert.Throws<InvalidOperationException>(() =>
            ruta.QuitarEnvio(primero, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ExtravioEnPreparacion_LiberaParadaYActualizaCapacidad()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var ahora = DateTimeOffset.UtcNow;

        VersionReglasOperativas? reglasPrimero = null;

        var primero = CrearEnvio(
            operadorId,
            capturarReglas: reglas => reglasPrimero = reglas);

        var segundo = CrearEnvio(operadorId);

        ruta.AgregarEnvio(primero);
        ruta.AgregarEnvio(segundo);

        primero.Aplicar(
            DisparadorEnvio.AsignarARuta,
            reglasPrimero!,
            ahora,
            "Prueba",
            "OperarioTest");

        var pesoInicial = ruta.PesoAsignadoKg;
        var volumenInicial = ruta.VolumenAsignadoM3;

        ruta.LiberarParadaPorExtravio(primero, ahora);
        ruta.PrepararRenumeracionParadas();
        ruta.RenumerarParadasActivas();

        var paradaLiberada = ruta.Paradas.Single(
            p => p.EnvioId == primero.Id);

        var paradaActiva = ruta.Paradas.Single(
            p => p.EnvioId == segundo.Id);

        Assert.False(paradaLiberada.Activa);
        Assert.True(paradaActiva.Activa);
        Assert.Equal(1, paradaActiva.Orden);

        Assert.Equal(
            pesoInicial - primero.PesoTotalKg,
            ruta.PesoAsignadoKg);

        Assert.Equal(
            volumenInicial - primero.VolumenTotalMetrosCubicos,
            ruta.VolumenAsignadoM3);

        Assert.Equal(EstadoHojaDeRuta.EnPreparacion, ruta.Estado);
    }

    [Fact]
    public void ExtravioEnHojaDespachada_LiberaParadaSinRenumerar()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var ahora = DateTimeOffset.UtcNow;

        VersionReglasOperativas? reglasPrimero = null;

        var primero = CrearEnvio(
            operadorId,
            capturarReglas: reglas => reglasPrimero = reglas);

        var segundo = CrearEnvio(operadorId);

        ruta.AgregarEnvio(primero);
        ruta.AgregarEnvio(segundo);

        primero.Aplicar(
            DisparadorEnvio.AsignarARuta,
            reglasPrimero!,
            ahora,
            "Prueba",
            "OperarioTest");

        ruta.MarcarDespachada(ahora);

        primero.Aplicar(
            DisparadorEnvio.Despachar,
            reglasPrimero!,
            ahora,
            "Prueba",
            "OperarioTest");

        var ordenSegundo = ruta.Paradas.Single(
            p => p.EnvioId == segundo.Id).Orden;

        var pesoInicial = ruta.PesoAsignadoKg;
        var volumenInicial = ruta.VolumenAsignadoM3;

        ruta.LiberarParadaPorExtravio(primero, ahora);

        var paradaLiberada = ruta.Paradas.Single(
            p => p.EnvioId == primero.Id);

        var paradaActiva = ruta.Paradas.Single(
            p => p.EnvioId == segundo.Id);

        Assert.False(paradaLiberada.Activa);
        Assert.True(paradaActiva.Activa);

        Assert.Equal(ordenSegundo, paradaActiva.Orden);
        Assert.Equal(EstadoHojaDeRuta.Despachada, ruta.Estado);

        Assert.Equal(
            pesoInicial - primero.PesoTotalKg,
            ruta.PesoAsignadoKg);

        Assert.Equal(
            volumenInicial - primero.VolumenTotalMetrosCubicos,
            ruta.VolumenAsignadoM3);
    }

    [Fact]
    public void EntregaRetiroComercio_CreaParadaCorrecta()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var retiro = CrearRetiroEnDeposito(operadorId);

        ruta.AgregarEntregaRetiroComercio(retiro);

        var parada = Assert.Single(ruta.Paradas);

        Assert.Equal(
            TipoOperacionRuta.EntregaRetiroComercio,
            parada.TipoOperacion);

        Assert.Equal(retiro.Id, parada.RetiroId);
        Assert.True(parada.Activa);
        Assert.Equal(retiro.PesoTotalKg, ruta.PesoAsignadoKg);
    }

    [Fact]
    public void EntregaRetiroComercio_NoFinalizaAntesDeConfirmar()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var retiro = CrearRetiroEnDeposito(operadorId);

        ruta.AgregarEntregaRetiroComercio(retiro);
        ruta.MarcarDespachada(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            ruta.FinalizarParadaEntregaRetiroComercio(
                retiro,
                DateTimeOffset.UtcNow));

        Assert.True(Assert.Single(ruta.Paradas).Activa);
    }

    [Fact]
    public void EntregaRetiroComercio_ConfirmadaLiberaParada()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var retiro = CrearRetiroEnDeposito(operadorId);
        var ahora = DateTimeOffset.UtcNow;

        ruta.AgregarEntregaRetiroComercio(retiro);
        ruta.MarcarDespachada(ahora);

        retiro.EntregarAlComercio();

        ruta.FinalizarParadaEntregaRetiroComercio(
            retiro,
            ahora);

        Assert.Equal(
            EstadoRetiro.EntregadoAlComercio,
            retiro.Estado);

        Assert.False(Assert.Single(ruta.Paradas).Activa);
    }   

    [Fact]
    public void DevolucionComercio_CreaParadaCorrecta()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var (envio, _) = CrearEnvioEnDevolucion(operadorId);

        ruta.AgregarDevolucionComercio(envio);

        var parada = Assert.Single(ruta.Paradas);

        Assert.Equal(
            TipoOperacionRuta.DevolucionComercio,
            parada.TipoOperacion);

        Assert.Equal(envio.Id, parada.EnvioId);
        Assert.True(parada.Activa);

        Assert.Equal(
            envio.Bultos
                .Where(b => b.Estado == EstadoBulto.EnDevolucion)
                .Sum(b => b.PesoKg),
            ruta.PesoAsignadoKg);
    }

    [Fact]
    public void DevolucionComercio_NoFinalizaSinConfirmacion()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var (envio, _) = CrearEnvioEnDevolucion(operadorId);
        var ahora = DateTimeOffset.UtcNow;

        ruta.AgregarDevolucionComercio(envio);
        ruta.MarcarDespachada(ahora);

        Assert.Throws<InvalidOperationException>(() =>
            ruta.FinalizarParada(envio, ahora));

        Assert.True(Assert.Single(ruta.Paradas).Activa);
    }

    [Fact]
    public void DevolucionComercio_ConfirmadaLiberaParada()
    {
        var operadorId = Guid.NewGuid();
        var ruta = CrearRuta(operadorId);
        var (envio, reglas) = CrearEnvioEnDevolucion(operadorId);
        var ahora = DateTimeOffset.UtcNow;

        ruta.AgregarDevolucionComercio(envio);
        ruta.MarcarDespachada(ahora);

        envio.Aplicar(
            DisparadorEnvio.ConfirmarDevolucion,
            reglas,
            ahora,
            "PortalComercio",
            "ComercioTest",
            modalidadDevolucion: ModalidadDevolucion.EntregaDirecta,
            receptorDevolucion: "Responsable del comercio",
            referenciaConstanciaDevolucion: "CONST-001");

        ruta.FinalizarParada(envio, ahora);

        Assert.Equal(EstadoEnvio.Devuelto, envio.Estado);
        Assert.All(envio.Bultos,
            b => Assert.Equal(EstadoBulto.Devuelto, b.Estado));

        Assert.False(Assert.Single(ruta.Paradas).Activa);
    }

    [Fact]
    public void RutaMixta_EntregaDevolucionAntesDeRecogerRetiro()
    {
        var operadorId = Guid.NewGuid();

        var ruta = CrearRuta(
            operadorId,
            capacidadPeso: 10m);

        ruta.ConfigurarHoraSalida(new TimeOnly(8, 0));

        var (envio, _) = CrearEnvioEnDevolucion(operadorId);

        var retiro = Retiro.Crear(
            operadorId,
            Guid.NewGuid(),
            "Retiro de prueba",
            new Direccion("Origen", "100", "San José", "San José"),
            new Direccion("Comercio", "200", "San José", "San José"),
            ruta.Fecha,
            new TimeOnly(11, 0),
            new TimeOnly(12, 0),
            [BultoRetiro.Crear("RET-MIXTO", 9m, 20, 20, 20)],
            DateTimeOffset.UtcNow);

        var zona = ZonaCobertura.Crear(
            operadorId,
            "San José",
            "San José");

        zona.AgregarLocalidad("San José");
        zona.AgregarFranja(
            new TimeOnly(9, 0),
            new TimeOnly(10, 0));
        zona.AgregarFranja(
            new TimeOnly(11, 0),
            new TimeOnly(12, 0));
        zona.ConfigurarTiempoEstimado(10);

        var horarios = PlanificacionHojaDeRuta.Calcular(
            ruta,
            Array.Empty<EnvioParaPlanificar>(),
            [new RetiroParaPlanificar(retiro, zona)],
            [new DevolucionParaPlanificar(
                envio,
                zona,
                new Direccion("Comercio", "200", "San José", "San José"))],
            Array.Empty<EntregaRetiroParaPlanificar>(),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0));

        Assert.Equal(2, horarios.Count);

        Assert.Equal(
            TipoOperacionRuta.DevolucionComercio,
            horarios[0].Tipo);

        Assert.Equal(
            TipoOperacionRuta.Retiro,
            horarios[1].Tipo);

        // Después de entregar la devolución,
        // el vehículo queda vacío.
        Assert.Equal(0m, horarios[0].PesoPosteriorKg);

        // Después de recoger el retiro,
        // transporta los 9 kg recogidos.
        Assert.Equal(9m, horarios[1].PesoPosteriorKg);
    }

}
