using System.Net;
using MassTransit;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.Eventos;

namespace UltimaMilla.Worker.Consumidores;

/// <summary>
/// Manda al destinatario el enlace público de seguimiento (letra 3.3): no tiene cuenta y lo recibe por correo.
/// No toca la base, así que no fija el tenant ni usa el outbox.
/// </summary>
public sealed class EnviarEnlaceSeguimientoConsumer(IEnviadorCorreo correo, IConfiguration configuration)
    : IConsumer<EnlaceSeguimientoSolicitado>
{
    public async Task Consume(ConsumeContext<EnlaceSeguimientoSolicitado> context)
    {
        var m = context.Message;
        var urlBase = (configuration["Seguimiento:UrlBase"] ?? "http://portal.localhost:8000/seguimiento").TrimEnd('/');
        var enlace = $"{urlBase}/{m.TokenSeguimiento}";

        var cuerpo =
            $"<p>Hola {WebUtility.HtmlEncode(m.NombreDestinatario)}:</p>" +
            $"<p>Tu pedido <strong>{WebUtility.HtmlEncode(m.ReferenciaExterna)}</strong> está en camino. " +
            "Podés seguirlo en este enlace:</p>" +
            $"<p><a href=\"{enlace}\">{enlace}</a></p>";

        await correo.EnviarAsync(m.Email, $"Seguí tu pedido {m.ReferenciaExterna}", cuerpo, context.CancellationToken);
    }
}

public sealed class EnviarEnlaceSeguimientoDefinition : ConsumerDefinition<EnviarEnlaceSeguimientoConsumer>
{
    public EnviarEnlaceSeguimientoDefinition()
    {
        EndpointName = "enviar-enlace-seguimiento";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<EnviarEnlaceSeguimientoConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Si el SMTP no responde: 5 intentos con espera creciente y después a la cola _error.
        endpointConfigurator.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
    }
}
