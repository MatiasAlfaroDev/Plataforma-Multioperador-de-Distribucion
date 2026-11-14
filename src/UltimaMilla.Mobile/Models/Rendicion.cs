namespace UltimaMilla.Mobile.Models;

/// <summary>Respuesta de POST /api/repartidor/hojas-de-ruta/{id}/carga (RF 19).</summary>
/// <param name="Motivo">Si el servidor no pudo verificar (por ejemplo, la hoja no está despachada), por qué.</param>
public sealed record VerificacionCarga(
    bool Completa,
    IReadOnlyList<string> Cargados,
    IReadOnlyList<string> Faltantes,
    IReadOnlyList<string> Sobrantes,
    string? Motivo = null);

/// <summary>Lo que devuelve GET /api/repartidor/hojas-de-ruta/{id}/rendicion (mismos nombres que RendicionDto).</summary>
/// <param name="ParadasSinResultado">Paradas que el servidor todavía no tiene registradas: hay que resolverlas antes de rendir.</param>
public sealed record RendicionItem(
    Guid HojaDeRutaId,
    string Estado,
    IReadOnlyList<RetornoEsperadoItem> Retornos,
    IReadOnlyList<string> ParadasSinResultado);

/// <param name="Tipo">"Envio" (no entregado o con bultos pendientes de una entrega parcial) o "Retiro" (bultos retirados en domicilios).</param>
public sealed record RetornoEsperadoItem(string Tipo, Guid Id, string Referencia, IReadOnlyList<string> Bultos);
