
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

public interface IReporteEnviosRepository
{
    Task<IReadOnlyList<ConteoEnviosPorEstado>> ContarPorEstadoAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct);

    Task<IReadOnlyList<ConteoMotivoNoEntrega>> ContarMotivosNoEntregaAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct);

    Task<IReadOnlyList<VolumenDiarioReporte>> ObtenerVolumenDiarioAsync(
    Guid operadorId,
    DateTimeOffset desde,
    DateTimeOffset hastaExclusivo,
    CancellationToken ct);

    Task<IReadOnlyList<ResumenComercioReporte>> ObtenerPorComercioAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct);

    Task<IReadOnlyList<ResultadoRepartidorReporte>> ObtenerResultadosPorRepartidorAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct);

    Task<CumplimientoReporte> ObtenerCumplimientoAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct);

    Task<IReadOnlyList<DetalleLiquidacionComercio>>ObtenerDetalleLiquidacionAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct);
}

public sealed record ConteoEnviosPorEstado(
    EstadoEnvio Estado,
    int Cantidad);

public sealed record ConteoMotivoNoEntrega(
    string Codigo,
    string Descripcion,
    int IntentosFallidos,
    int BultosNoEntregados);

public sealed record VolumenDiarioReporte(
    DateOnly Fecha,
    int Cantidad);

public sealed record ResumenComercioReporte(
    Guid CuentaComercialId,
    string Comercio,
    int TotalEnvios,
    int Entregados,
    int EntregadosParcialmente,
    decimal ImporteTarifado);

public sealed record ResultadoRepartidorReporte(
    Guid? RepartidorId,
    string Repartidor,
    int EntregasCompletas,
    int EntregasParciales,
    int IntentosFallidos);


public sealed record CumplimientoReporte(
    IReadOnlyList<CumplimientoZonaItem> Zonas,
    IReadOnlyList<CumplimientoComercioItem> Comercios);

public sealed record CumplimientoZonaItem(
    Guid? ZonaId,
    string Zona,
    int Completas,
    int Parciales,
    int Fallidas,
    decimal PorcentajeCompletas);

public sealed record CumplimientoComercioItem(
    Guid CuentaComercialId,
    string Comercio,
    int Completas,
    int Parciales,
    int Fallidas,
    decimal PorcentajeCompletas);


public sealed record DetalleLiquidacionEnvio(
    Guid EnvioId,
    string Referencia,
    DateTimeOffset FechaAlta,
    string Estado,
    decimal? Tarifa);

public sealed record DetalleLiquidacionComercio(
    Guid CuentaComercialId,
    string Comercio,
    int TotalEnvios,
    int EnviosSinTarifa,
    decimal TotalTarifado,
    IReadOnlyList<DetalleLiquidacionEnvio> Envios);
