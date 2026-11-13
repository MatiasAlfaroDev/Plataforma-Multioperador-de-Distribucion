using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Tablero.Queries.ObtenerTableroEnVivo;

// Los estados viajan como texto, igual que en el listado de envíos.
public sealed record ConteoEstadoDto(string Estado, int Cantidad);

public sealed record EnvioEnRiesgoDto(
    Guid EnvioId, string Referencia, string Estado, string Destinatario, string Direccion,
    DateOnly Fecha, TimeOnly HoraDesde, TimeOnly HoraHasta, int MinutosRestantes, bool Vencido);

public sealed record TableroEnVivoDto(
    IReadOnlyList<ConteoEstadoDto> Estados,
    IReadOnlyList<PosicionFlotaDto> Flota,
    IReadOnlyList<EnvioEnRiesgoDto> EnRiesgo,
    DateTimeOffset GeneradoEn);

/// <summary>
/// Foto del tablero de operación en vivo (RF 29): envíos por estado, posición de la flota y envíos en riesgo de
/// incumplir su ventana. El Backoffice la vuelve a pedir cada vez que SignalR avisa un cambio (RNF 6.10).
/// </summary>
public sealed class ObtenerTableroEnVivoHandler(ITableroRepository tablero, TimeProvider reloj)
{
    // ponytail: margen fijo; pasarlo a las reglas operativas si cada operador quiere el suyo.
    public static readonly TimeSpan MargenDeRiesgo = TimeSpan.FromMinutes(60);

    // Solo las posiciones recientes: un repartidor que no reporta hace horas no está en la calle.
    private static readonly TimeSpan AntiguedadFlota = TimeSpan.FromHours(2);

    private static readonly TimeZoneInfo ZonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public async Task<TableroEnVivoDto> Handle(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(ahora, ZonaHoraria).DateTime;

        var estados = await tablero.ContarPorEstadoAsync(ct);
        var flota = await tablero.UltimasPosicionesAsync(ahora - AntiguedadFlota, ct);
        var pendientes = await tablero.PendientesConVentanaHastaAsync(DateOnly.FromDateTime(local), ct);

        var enRiesgo = pendientes
            .Select(e => (Envio: e, Restante: e.Fecha.ToDateTime(e.HoraHasta) - local))
            .Where(x => EnRiesgo(x.Restante))
            .OrderBy(x => x.Restante)
            .Select(x => new EnvioEnRiesgoDto(
                x.Envio.EnvioId, x.Envio.Referencia, x.Envio.Estado.ToString(), x.Envio.Destinatario, x.Envio.Direccion,
                x.Envio.Fecha, x.Envio.HoraDesde, x.Envio.HoraHasta,
                (int)Math.Floor(x.Restante.TotalMinutes), x.Restante < TimeSpan.Zero))
            .ToList();

        return new TableroEnVivoDto(
            estados.Select(e => new ConteoEstadoDto(e.Estado.ToString(), e.Cantidad)).ToList(), flota, enRiesgo, ahora);
    }

    /// <summary>En riesgo: a la ventana le queda menos que el margen, o ya cerró sin entrega.</summary>
    public static bool EnRiesgo(TimeSpan restanteHastaElCierre) => restanteHastaElCierre <= MargenDeRiesgo;
}
