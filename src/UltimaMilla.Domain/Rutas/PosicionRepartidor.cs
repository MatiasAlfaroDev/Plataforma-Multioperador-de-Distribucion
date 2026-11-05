using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Rutas;

/// <summary>
/// Dónde estaba el repartidor en un momento de la jornada (RF 23 y 6.11: seguimiento de la
/// posición con la app en segundo plano y la pantalla apagada).
/// </summary>
/// <remarks>
/// Igual que <c>EventoMovil</c>: el Id lo genera el teléfono al tomar la posición. Si la app
/// reenvía un lote (se cortó la conexión antes de la respuesta), las posiciones repetidas se
/// reconocen por el Id y no se guardan dos veces.
/// </remarks>
public sealed class PosicionRepartidor : AuditableEntity, ITenantEntity
{
    /// <summary>Tolerancia para relojes de teléfono un poco adelantados.</summary>
    private static readonly TimeSpan ToleranciaReloj = TimeSpan.FromMinutes(5);

    public Guid OperadorId { get; private set; }
    public Guid RepartidorId { get; private set; }
    public decimal Latitud { get; private set; }
    public decimal Longitud { get; private set; }

    /// <summary>Cuándo la tomó el teléfono (puede ser horas antes de llegar, si estuvo sin señal).</summary>
    public DateTimeOffset RegistradaEn { get; private set; }

    private PosicionRepartidor() { }

    public static PosicionRepartidor Registrar(
        Guid idPosicion,
        Guid operadorId,
        Guid repartidorId,
        decimal latitud,
        decimal longitud,
        DateTimeOffset registradaEn,
        DateTimeOffset ahora)
    {
        if (idPosicion == Guid.Empty)
            throw new ArgumentException("La posición necesita el identificador que generó el teléfono.", nameof(idPosicion));

        if (latitud is < -90 or > 90 || longitud is < -180 or > 180)
            throw new ArgumentException("Las coordenadas no son válidas.");

        if (registradaEn > ahora + ToleranciaReloj)
            throw new ArgumentException("La posición no puede ser del futuro.", nameof(registradaEn));

        return new PosicionRepartidor
        {
            Id = idPosicion,
            OperadorId = operadorId,
            RepartidorId = repartidorId,
            Latitud = latitud,
            Longitud = longitud,
            RegistradaEn = registradaEn
        };
    }
}
