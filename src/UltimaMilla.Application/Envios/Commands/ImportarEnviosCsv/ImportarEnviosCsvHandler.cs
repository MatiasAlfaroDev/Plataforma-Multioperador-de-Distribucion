
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FluentValidation;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;

namespace UltimaMilla.Application.Envios.Commands.ImportarEnviosCsv;

public sealed class ImportarEnviosCsvHandler(
    CrearEnvioHandler crearEnvio)
{
    private sealed record FilaCsv(
        int Numero,
        string Referencia,
        string Modalidad,
        string Nombre,
        string? Telefono,
        string Calle,
        string NumeroPuerta,
        string Localidad,
        string Departamento,
        BultoCommand? Bulto,
        string? Error);

    public async Task<ResultadoImportacionCsv> Handle(
        Guid cuentaComercialId,
        TextReader contenido,
        CancellationToken ct)
    {
        if (cuentaComercialId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta comercial es obligatoria.");

        ArgumentNullException.ThrowIfNull(contenido);

        var configuracion = new CsvConfiguration(
            CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(contenido, configuracion);

        if (!await csv.ReadAsync())
            throw new ArgumentException("El archivo CSV está vacío.");

        csv.ReadHeader();

        string[] obligatorias =
        [
            "ReferenciaExterna",
            "Modalidad",
            "DestinatarioNombre",
            "Calle",
            "Numero",
            "Localidad",
            "Departamento",
            "PesoKg",
            "AltoCm",
            "AnchoCm",
            "ProfundidadCm"
        ];

        var encabezados = csv.HeaderRecord ?? [];

        var faltantes = obligatorias
            .Where(h => !encabezados.Contains(
                h, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (faltantes.Length > 0)
            throw new ArgumentException(
                "Faltan columnas: " + string.Join(", ", faltantes));

        var filas = new List<FilaCsv>();

        string Campo(string nombre) =>
            csv.GetField(nombre)?.Trim() ?? "";

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            var numero = csv.Context.Parser?.Row
                ?? throw new InvalidOperationException(
                    "No se pudo determinar el número de fila del CSV.");

            var referencia = Campo("ReferenciaExterna");
            var pesoTexto = Campo("PesoKg");
            var altoTexto = Campo("AltoCm");
            var anchoTexto = Campo("AnchoCm");
            var profundidadTexto = Campo("ProfundidadCm");

            var pesoValido = decimal.TryParse(
                pesoTexto,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var peso);

            var altoValido = int.TryParse(
                altoTexto,
                out var alto);

            var anchoValido = int.TryParse(
                anchoTexto,
                out var ancho);

            var profundidadValida = int.TryParse(
                profundidadTexto,
                out var profundidad);

            var dimensionesValidas =
                pesoValido && altoValido &&
                anchoValido && profundidadValida;

            var bulto = dimensionesValidas
                ? new BultoCommand(
                    peso, alto, ancho, profundidad)
                : null;

            filas.Add(new FilaCsv(
                numero,
                referencia,
                Campo("Modalidad"),
                Campo("DestinatarioNombre"),
                encabezados.Contains("DestinatarioTelefono")
                    ? Campo("DestinatarioTelefono")
                    : null,
                Campo("Calle"),
                Campo("Numero"),
                Campo("Localidad"),
                Campo("Departamento"),
                bulto,
                dimensionesValidas
                    ? null
                    : "Peso o dimensiones inválidas."));
        }

        var resultados = new List<ResultadoEnvioImportado>();

        // Las filas sin referencia no pueden agruparse.
        foreach (var fila in filas.Where(
            f => string.IsNullOrWhiteSpace(f.Referencia)))
        {
            resultados.Add(new ResultadoEnvioImportado(
                "",
                fila.Numero,
                EstadoImportacionCsv.Error,
                null,
                "La referencia externa es obligatoria."));
        }

        var grupos = filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Referencia))
            .GroupBy(
                f => f.Referencia,
                StringComparer.OrdinalIgnoreCase);

        foreach (var grupo in grupos)
        {
            ct.ThrowIfCancellationRequested();

            var primera = grupo.First();

            if (grupo.Any(f => f.Error is not null))
            {
                resultados.Add(new ResultadoEnvioImportado(
                    grupo.Key,
                    primera.Numero,
                    EstadoImportacionCsv.Error,
                    null,
                    "Alguna fila contiene un bulto inválido."));

                continue;
            }

            // Todos los bultos de una referencia deben
            // compartir los datos del envío.
            var inconsistente = grupo.Any(f =>
                f.Modalidad != primera.Modalidad ||
                f.Nombre != primera.Nombre ||
                f.Telefono != primera.Telefono ||
                f.Calle != primera.Calle ||
                f.NumeroPuerta != primera.NumeroPuerta ||
                f.Localidad != primera.Localidad ||
                f.Departamento != primera.Departamento);

            if (inconsistente)
            {
                resultados.Add(new ResultadoEnvioImportado(
                    grupo.Key,
                    primera.Numero,
                    EstadoImportacionCsv.Error,
                    null,
                    "Las filas del envío contienen datos diferentes."));

                continue;
            }

            var comando = new CrearEnvioCommand(
                cuentaComercialId,
                primera.Referencia,
                primera.Modalidad,
                primera.Nombre,
                primera.Telefono,
                primera.Calle,
                primera.NumeroPuerta,
                primera.Localidad,
                primera.Departamento,
                grupo.Select(f => f.Bulto!).ToArray());

            try
            {
                var id = await crearEnvio.Handle(comando, ct);

                resultados.Add(new ResultadoEnvioImportado(
                    grupo.Key,
                    primera.Numero,
                    EstadoImportacionCsv.Creado,
                    id,
                    null));
            }
            catch (ConflictoException)
            {
                resultados.Add(new ResultadoEnvioImportado(
                    grupo.Key,
                    primera.Numero,
                    EstadoImportacionCsv.YaExistente,
                    null,
                    "Ya existe un envío con esta referencia."));
            }
            catch (Exception ex) when (
                ex is FluentValidation.ValidationException ||
                ex is ArgumentException ||
                ex is InvalidOperationException ||
                ex is NoEncontradoException)
            {
                resultados.Add(new ResultadoEnvioImportado(
                    grupo.Key,
                    primera.Numero,
                    EstadoImportacionCsv.Error,
                    null,
                    ex.Message));
            }
        }

        return new ResultadoImportacionCsv(
            resultados.Count,
            resultados.Count(r =>
                r.Estado == EstadoImportacionCsv.Creado),
            resultados.Count(r =>
                r.Estado == EstadoImportacionCsv.YaExistente),
            resultados.Count(r =>
                r.Estado == EstadoImportacionCsv.Error),
            resultados);
    }
}
