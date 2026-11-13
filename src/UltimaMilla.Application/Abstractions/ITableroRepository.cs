using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Lecturas del tablero de operación en vivo (RF 29). Filtradas por operador como todo lo demás.</summary>
public interface ITableroRepository
{
    Task<IReadOnlyList<ConteoEstado>> ContarPorEstadoAsync(CancellationToken ct);

    /// <summary>La última posición de cada repartidor registrada desde ese momento.</summary>
    Task<IReadOnlyList<PosicionFlotaDto>> UltimasPosicionesAsync(DateTimeOffset desde, CancellationToken ct);

    /// <summary>Envíos sin entregar con ventana comprometida hasta esa fecha inclusive.</summary>
    Task<IReadOnlyList<EnvioConVentana>> PendientesConVentanaHastaAsync(DateOnly hasta, CancellationToken ct);
}

public sealed record ConteoEstado(EstadoEnvio Estado, int Cantidad);

public sealed record PosicionFlotaDto(
    Guid RepartidorId, string Repartidor, decimal Latitud, decimal Longitud, DateTimeOffset RegistradaEn);

public sealed record EnvioConVentana(
    Guid EnvioId, string Referencia, EstadoEnvio Estado, string Destinatario, string Direccion,
    DateOnly Fecha, TimeOnly HoraDesde, TimeOnly HoraHasta);
