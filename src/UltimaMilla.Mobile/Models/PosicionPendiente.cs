namespace UltimaMilla.Mobile.Models;

/// <summary>Una posición tomada durante la jornada que todavía no llegó al servidor.</summary>
/// <param name="Id">Lo genera el teléfono: si el lote se reenvía, el servidor no la guarda dos veces.</param>
public sealed record PosicionPendiente(Guid Id, decimal Latitud, decimal Longitud, DateTimeOffset RegistradaEn);
