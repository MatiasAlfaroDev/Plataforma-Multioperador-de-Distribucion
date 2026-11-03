
using UltimaMilla.Application.Envios.Commands.ImportarEnviosCsv;

namespace UltimaMilla.UnitTests;

public class ImportarEnviosCsvTests
{
    private static readonly Guid CuentaId = Guid.NewGuid();

    private const string Encabezado =
        "ReferenciaExterna,Modalidad,DestinatarioNombre," +
        "Calle,Numero,Localidad,Departamento," +
        "PesoKg,AltoCm,AnchoCm,ProfundidadCm";

    [Fact]
    public async Task Archivo_vacio_es_rechazado()
    {
        var handler = new ImportarEnviosCsvHandler(null!);
        using var lector = new StringReader("");

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(CuentaId, lector, CancellationToken.None));

        Assert.Contains("vacío", error.Message);
    }

    [Fact]
    public async Task Columnas_obligatorias_faltantes_son_rechazadas()
    {
        var handler = new ImportarEnviosCsvHandler(null!);
        using var lector = new StringReader(
            "ReferenciaExterna,Modalidad\nPED-001,Estandar");

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(CuentaId, lector, CancellationToken.None));

        Assert.Contains("Faltan columnas", error.Message);
    }

    [Fact]
    public async Task Referencia_vacia_se_informa_como_error()
    {
        var handler = new ImportarEnviosCsvHandler(null!);

        var contenido = Encabezado + "\n" +
            ",Estandar,Ana Perez,Artigas,123," +
            "San José de Mayo,San José,2.5,20,30,40";

        using var lector = new StringReader(contenido);

        var resultado = await handler.Handle(
            CuentaId, lector, CancellationToken.None);

        Assert.Equal(1, resultado.TotalEnvios);
        Assert.Equal(0, resultado.Creados);
        Assert.Equal(1, resultado.ConErrores);
        Assert.Equal(
            EstadoImportacionCsv.Error,
            resultado.Resultados[0].Estado);
    }

    [Fact]
    public async Task Dimensiones_no_numericas_son_rechazadas()
    {
        var handler = new ImportarEnviosCsvHandler(null!);

        var contenido = Encabezado + "\n" +
            "PED-001,Estandar,Ana Perez,Artigas,123," +
            "San José de Mayo,San José,abc,20,30,40";

        using var lector = new StringReader(contenido);

        var resultado = await handler.Handle(
            CuentaId, lector, CancellationToken.None);

        Assert.Equal(1, resultado.TotalEnvios);
        Assert.Equal(0, resultado.Creados);
        Assert.Equal(1, resultado.ConErrores);
    }
}
