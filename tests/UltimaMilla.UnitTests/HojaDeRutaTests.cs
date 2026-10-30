
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;
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
        decimal peso = 2m)
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

            envio.Aplicar(
                DisparadorEnvio.RecibirEnDeposito,
                reglas,
                ahora,
                "Prueba",
                "OperarioTest");
        }

        return envio;
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
                .Select(p => p.EnvioId));
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

}
