namespace UltimaMilla.Mobile.Models;

/// <summary>Un bulto pendiente de un envío de la ruta, con el Id que espera el servidor (RF 32).</summary>
public sealed record BultoItem(Guid Id, string Identificacion);

/// <summary>Un bulto que no se entregó en una entrega parcial: motivo del catálogo y foto de evidencia.</summary>
public sealed record BultoNoEntregado(Guid BultoId, string Identificacion, string Motivo, string Evidencia);

/// <summary>GET /api/motivos-no-entrega.</summary>
public sealed record CatalogoMotivos(bool AdmiteEntregaParcial, List<string> Motivos);

/// <summary>Lo que devuelve la pantalla de bultos: cuáles se entregan y cuáles no.</summary>
public sealed record SeleccionBultos(IReadOnlyList<BultoItem> Entregados, IReadOnlyList<BultoItem> NoEntregados);
