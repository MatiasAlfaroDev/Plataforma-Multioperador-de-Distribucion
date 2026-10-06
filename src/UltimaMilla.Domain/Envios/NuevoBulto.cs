namespace UltimaMilla.Domain.Envios;

/// <summary>Datos para crear un bulto dentro de un envío.</summary>
public sealed record NuevoBulto(decimal PesoKg, int AltoCm, int AnchoCm, int ProfundidadCm);
