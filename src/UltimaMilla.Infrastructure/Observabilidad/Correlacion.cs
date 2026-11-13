using System.Diagnostics;
using System.Text.RegularExpressions;
using MassTransit;
using LogContext = Serilog.Context.LogContext;

namespace UltimaMilla.Infrastructure.Observabilidad;

/// <summary>
/// Identificador de correlación de punta a punta (RNF 6.12): lo genera la app al sincronizar, la API lo toma
/// del encabezado, viaja en los mensajes del outbox y el Worker lo vuelve a fijar al consumir. Mientras dura
/// la operación está en todos los logs (propiedad CorrelationId) y en la span actual (correlation.id).
/// </summary>
public static partial class Correlacion
{
    public const string Encabezado = "X-Correlation-Id";

    private static readonly AsyncLocal<string?> actual = new();

    public static string? Actual => actual.Value;

    /// <summary>Fija el identificador para el resto del flujo asíncrono. Si no llega uno válido, genera otro.</summary>
    public static IDisposable Iniciar(string? id)
    {
        // Llega del cliente y termina en los logs: solo se acepta un formato acotado.
        if (string.IsNullOrEmpty(id) || !Formato().IsMatch(id))
            id = Guid.NewGuid().ToString("N");

        actual.Value = id;
        Activity.Current?.SetTag("correlation.id", id);
        return LogContext.PushProperty("CorrelationId", id);
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex Formato();
}

/// <summary>Copia el identificador de la operación en curso al encabezado del mensaje que se publica.</summary>
public sealed class CorrelacionAlPublicar<T> : IFilter<PublishContext<T>> where T : class
{
    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        // El outbox vuelve a pasar por acá al entregar, ya sin operación en curso: no pisar lo que trae.
        if (Correlacion.Actual is { } id && !context.Headers.TryGetHeader(Correlacion.Encabezado, out _))
            context.Headers.Set(Correlacion.Encabezado, id);

        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlacion");
}

/// <summary>Fija en el consumidor el identificador que trae el mensaje.</summary>
public sealed class CorrelacionAlConsumir<T> : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        using var _ = Correlacion.Iniciar(context.Headers.Get<string>(Correlacion.Encabezado));
        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlacion");
}
