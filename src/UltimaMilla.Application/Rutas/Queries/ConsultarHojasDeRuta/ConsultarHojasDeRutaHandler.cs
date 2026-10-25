
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;

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
    IZonaCoberturaRepository zonas)
{
    public async Task<OpcionesHojaDeRutaDto> ObtenerOpcionesAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        var listaRepartidores = await repartidores.ListarActivosAsync(
            operadorId, ct);

        var listaVehiculos = await vehiculos.ListarActivosAsync(
            operadorId, ct);

        var listaEnvios = await envios.ListarAsync(
            operadorId,
            null,
            ct);

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

        // Envíos disponibles para asignar a una ruta.
        var enviosDisponibles = listaEnvios
            .Where(x => x.Estado == EstadoEnvio.EnDeposito)
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
