using System.Text.Json.Serialization;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;

/// <summary>Una diferencia entre lo declarado por el comercio y lo escaneado en el depósito.</summary>
public sealed record DiscrepanciaRecepcionDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TipoDiscrepanciaRecepcion Tipo,
    string Identificacion,
    string Responsable,
    DateTimeOffset RegistradaEn);

/// <summary>
/// Resultado de escanear un envío en el depósito. <c>Recibido</c> es false cuando faltan bultos
/// declarados: el envío sigue Admitido y las discrepancias quedan registradas igual.
/// </summary>
public sealed record ResultadoRecepcion(
    bool Recibido,
    IReadOnlyList<DiscrepanciaRecepcionDto> Discrepancias);
