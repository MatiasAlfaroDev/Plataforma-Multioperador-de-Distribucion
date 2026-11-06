
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record EstadoEnvioReporteItem(
    string Estado,
    int Cantidad);

public sealed record ResumenEnviosReporte(
    DateOnly Desde,
    DateOnly Hasta,
    int TotalEnvios,
    List<EstadoEnvioReporteItem> PorEstado);

public sealed record MotivoNoEntregaReporteItem(
    string Codigo,
    string Descripcion,
    int IntentosFallidos,
    int BultosNoEntregados,
    int TotalIncidencias);

public sealed record MotivosNoEntregaReporte(
    DateOnly Desde,
    DateOnly Hasta,
    int TotalIncidencias,
    List<MotivoNoEntregaReporteItem> Motivos);

public sealed record VolumenDiarioItem(
    DateOnly Fecha,
    int Cantidad);

public sealed record ResumenComercioItem(
    Guid CuentaComercialId,
    string Comercio,
    int TotalEnvios,
    int Entregados,
    int EntregadosParcialmente,
    decimal ImporteTarifado);

public sealed record ActividadComercialReporte(
    DateOnly Desde,
    DateOnly Hasta,
    List<VolumenDiarioItem> VolumenDiario,
    List<ResumenComercioItem> Comercios);
public sealed record ResultadoRepartidorItem(
    Guid? RepartidorId,
    string Repartidor,
    int EntregasCompletas,
    int EntregasParciales,
    int IntentosFallidos);

public sealed record ResultadosRepartidoresReporte(
    DateOnly Desde,
    DateOnly Hasta,
    List<ResultadoRepartidorItem> Repartidores);

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

public sealed record CumplimientoResultados(
    List<CumplimientoZonaItem> Zonas,
    List<CumplimientoComercioItem> Comercios);

public sealed record CumplimientoReporteDto(
    DateOnly Desde,
    DateOnly Hasta,
    CumplimientoResultados Resultados);

public sealed record DetalleLiquidacionEnvioItem(
    Guid EnvioId,
    string Referencia,
    DateTimeOffset FechaAlta,
    string Estado,
    decimal? Tarifa);

public sealed record DetalleLiquidacionComercioItem(
    Guid CuentaComercialId,
    string Comercio,
    int TotalEnvios,
    int EnviosSinTarifa,
    decimal TotalTarifado,
    List<DetalleLiquidacionEnvioItem> Envios);

public sealed record DetalleLiquidacionReporte(
    DateOnly Desde,
    DateOnly Hasta,
    List<DetalleLiquidacionComercioItem> Comercios);


public sealed class ReportesApiClient(HttpClient http)
{
    public async Task<ResumenEnviosReporte> ObtenerResumenAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var url =
            $"api/reportes/envios/resumen?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";

        return await http.GetFromJsonAsync<ResumenEnviosReporte>(
            url, ct)
            ?? throw new InvalidOperationException(
                "La API no devolvió el resumen de envíos.");
    }

    public async Task<MotivosNoEntregaReporte> ObtenerMotivosAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var url =
            $"api/reportes/motivos-no-entrega?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";

        return await http.GetFromJsonAsync<MotivosNoEntregaReporte>(
            url, ct)
            ?? throw new InvalidOperationException(
                "La API no devolvió el reporte de motivos.");
    }

    public async Task<ActividadComercialReporte> ObtenerActividadAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var url =
            $"api/reportes/actividad-comercial?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";

        return await http.GetFromJsonAsync<ActividadComercialReporte>(
            url, ct)
            ?? throw new InvalidOperationException(
                "La API no devolvió la actividad comercial.");
    }

    public async Task<ResultadosRepartidoresReporte> ObtenerRepartidoresAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var url =
            $"api/reportes/repartidores?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";

        return await http.GetFromJsonAsync<ResultadosRepartidoresReporte>(url, ct)
            ?? throw new InvalidOperationException(
                "La API no devolvió el reporte de repartidores.");
    }
    public async Task<CumplimientoReporteDto> ObtenerCumplimientoAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var url =
            $"api/reportes/cumplimiento?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";

        return await http.GetFromJsonAsync<CumplimientoReporteDto>(
            url, ct)
            ?? throw new InvalidOperationException(
                "La API no devolvió el reporte de cumplimiento.");
    }
    
    public async Task<DetalleLiquidacionReporte> ObtenerLiquidacionAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        var url =
            $"api/reportes/liquidacion?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";

        return await http.GetFromJsonAsync<DetalleLiquidacionReporte>(
            url, ct)
            ?? throw new InvalidOperationException(
                "La API no devolvió el detalle de liquidación.");
    }

}
