
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Rutas.Queries.ConsultarHojasDeRuta;

public sealed record RepartidorRutaDto(
    Guid Id,
    string Nombre);

public sealed record VehiculoRutaDto(
    Guid Id,
    string Matricula,
    decimal CapacidadPesoKg,
    decimal CapacidadVolumenM3);

public sealed record EnvioRutaDto(
    Guid Id,
    string ReferenciaExterna,
    string Localidad,
    string Estado,
    decimal PesoKg,
    decimal VolumenM3,
    DateOnly? FechaComprometida,
    TimeOnly? HoraDesde,
    TimeOnly? HoraHasta,
    bool TieneCompromiso);

public sealed record FranjaRutaDto(
    TimeOnly HoraDesde,
    TimeOnly HoraHasta);

public sealed record OpcionesHojaDeRutaDto(
    IReadOnlyList<RepartidorRutaDto> Repartidores,
    IReadOnlyList<VehiculoRutaDto> Vehiculos,
    IReadOnlyList<EnvioRutaDto> Envios,
    IReadOnlyList<FranjaRutaDto> Franjas);

public sealed record HojaDeRutaDto(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    Guid RepartidorId,
    Guid VehiculoId,
    int CantidadParadas,
    decimal PesoAsignadoKg);

public sealed class ConsultarHojasDeRutaHandler(
    IRepartidorRepository repartidores,
    IVehiculoRepository vehiculos,
    IEnvioRepository envios,
    IHojaDeRutaRepository hojas,
    IZonaCoberturaRepository zonas,
    IVersionReglasOperativasRepository reglasRepository)
{
    public async Task<OpcionesHojaDeRutaDto> ObtenerOpcionesAsync(
        Guid operadorId,
        DateOnly? fecha,
        TimeOnly? horaDesde,
        TimeOnly? horaHasta,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        var listaRepartidores = await repartidores.ListarActivosAsync(
            operadorId, ct);

        var listaVehiculos = await vehiculos.ListarActivosAsync(
            operadorId, ct);

        var listaEnvios = await envios.ListarParaPlanificacionAsync(
            operadorId, ct);

        var zonasOperador = await zonas.ListarPorOperadorAsync(
            operadorId, ct);

        // Franjas configuradas en las zonas activas del operador.
        var franjas = zonasOperador
            .Where(z => z.Activa)
            .SelectMany(z => z.Franjas)
            .Select(f => new FranjaRutaDto(
                f.HoraDesde,
                f.HoraHasta))
            .Distinct()
            .OrderBy(f => f.HoraDesde)
            .ThenBy(f => f.HoraHasta)
            .ToList();
        var hayFiltros = fecha.HasValue ||
                 horaDesde.HasValue ||
                 horaHasta.HasValue;

        if (hayFiltros && (!fecha.HasValue || !horaDesde.HasValue || !horaHasta.HasValue)){
            throw new ArgumentException(
                "Para filtrar envíos deben indicarse fecha, hora de inicio y hora de fin.");
        }

        if (hayFiltros && horaDesde >= horaHasta){
            throw new ArgumentException(
                "La franja horaria no es válida.");
        }

        DateTimeOffset? inicioEntrega = null;

        if (hayFiltros){
            var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById(
                "America/Montevideo");

            var fechaHoraLocal = DateTime.SpecifyKind(
                fecha!.Value.ToDateTime(horaDesde!.Value),
                DateTimeKind.Unspecified);

            inicioEntrega = new DateTimeOffset(
                fechaHoraLocal,
                zonaHoraria.GetUtcOffset(fechaHoraLocal));
        }

        var elegibles = new List<Envio>();

        foreach (var envio in listaEnvios){
            if (!hayFiltros){
                elegibles.Add(envio);
                continue;
            }

            var zonasCompatibles = zonasOperador
                .Where(z => z.Activa &&
                            z.CubreDireccion(
                                envio.Direccion.Localidad, null))
                .ToList();

            if (zonasCompatibles.Count != 1 || !zonasCompatibles[0].OfreceFranja( horaDesde!.Value, horaHasta!.Value)){
                continue;
            }

            if (envio.Estado == EstadoEnvio.EnDeposito){
                if (envio.IntentosRealizados > 0)
                    continue;

                if (envio.FechaEntregaComprometida.HasValue &&
                    (envio.FechaEntregaComprometida != fecha ||
                    envio.HoraEntregaDesde != horaDesde ||
                    envio.HoraEntregaHasta != horaHasta)){
                    continue;
                }
            }
            else if (envio.Estado == EstadoEnvio.Reprogramado){
                if (envio.VersionReglasOperativasId is not Guid versionId)
                    continue;

                var reglas = await reglasRepository.ObtenerPorIdAsync(
                    operadorId, versionId, ct);

                if (reglas is null ||
                    envio.IntentosRealizados >= reglas.MaximoIntentosEntrega ||
                    envio.UltimoMotivoRequiereDevolucion){
                    continue;
                }

                var ultimoFallo = envio.Intentos
                    .Where(i => !i.Entregado)
                    .OrderByDescending(i => i.OcurridoEn)
                    .FirstOrDefault();

                if (ultimoFallo is null ||
                    inicioEntrega!.Value <
                    ultimoFallo.OcurridoEn.AddHours(
                        reglas.HorasMinimasEntreIntentos)){
                    continue;
                }
            }
            else
            {
                continue;
            }

            elegibles.Add(envio);
        }
        // Envíos disponibles para asignar a una ruta.
        var enviosDisponibles = elegibles
            .Where(x => x.Estado == EstadoEnvio.EnDeposito || x.Estado == EstadoEnvio.Reprogramado)
            .OrderBy(x => x.FechaEntregaComprometida)
            .ThenBy(x => x.HoraEntregaHasta)
            .Select(x => new EnvioRutaDto(
                x.Id,
                x.ReferenciaExterna,
                x.Direccion.Localidad,
                x.Estado.ToString(),
                x.PesoTotalKg,
                x.VolumenTotalMetrosCubicos,
                x.FechaEntregaComprometida,
                x.HoraEntregaDesde,
                x.HoraEntregaHasta,
                x.FechaEntregaComprometida.HasValue &&
                x.HoraEntregaDesde.HasValue &&
                x.HoraEntregaHasta.HasValue))
            .ToList();

        return new OpcionesHojaDeRutaDto(
            listaRepartidores
                .Select(x => new RepartidorRutaDto(
                    x.Id,
                    x.Nombre))
                .ToList(),

            listaVehiculos
                .Select(x => new VehiculoRutaDto(
                    x.Id,
                    x.Matricula,
                    x.CapacidadPesoKg,
                    x.CapacidadVolumenM3))
                .ToList(),

            enviosDisponibles,
            franjas);
    }

    public async Task<IReadOnlyList<HojaDeRutaDto>> ListarAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        var lista = await hojas.ListarAsync(
            operadorId, ct);

        return lista
            .Select(x => new HojaDeRutaDto(
                x.Id,
                x.Fecha,
                x.HoraSalida,
                x.RepartidorId,
                x.VehiculoId,
                x.Paradas.Count(p => p.Activa),
                x.PesoAsignadoKg))
            .ToList();
    }
}
