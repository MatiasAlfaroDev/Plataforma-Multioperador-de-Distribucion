namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Banderas de funcionalidad por operador (aclaración 3.2). Se leen de la caché compartida (Redis), así
/// un cambio llega a todas las réplicas de la API, al Worker y a las webs sin reiniciar nada.
/// </summary>
public interface IBanderas
{
    Task<bool> EstaHabilitadaAsync(string bandera, Guid operadorId, CancellationToken ct);
}

/// <summary>Las funcionalidades bajo bandera: las dos del cambio de requerimientos.</summary>
public static class Banderas
{
    public const string EntregaParcial = "entrega-parcial";
    public const string RetiroDomicilio = "retiro-domicilio";

    public static readonly IReadOnlyList<string> Todas = [EntregaParcial, RetiroDomicilio];
}
