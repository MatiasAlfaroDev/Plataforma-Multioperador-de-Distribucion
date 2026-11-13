namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Identificador de correlación (RNF 6.12): cada pedido a la API lleva X-Correlation-Id. Durante una
/// sincronización todos los pedidos comparten el mismo, así en Grafana se sigue esa sincronización desde
/// el teléfono hasta el Worker; fuera de ella, cada pedido lleva uno propio.
/// </summary>
public sealed class CorrelacionHandler : DelegatingHandler
{
    public const string Encabezado = "X-Correlation-Id";

    private static readonly AsyncLocal<string?> actual = new();

    /// <summary>Lo fija SincronizacionService al empezar; sigue a los pedidos que se hacen desde ahí.</summary>
    public static string? Actual
    {
        get => actual.Value;
        set => actual.Value = value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.TryAddWithoutValidation(Encabezado, Actual ?? Guid.NewGuid().ToString("N"));
        return base.SendAsync(request, ct);
    }
}
