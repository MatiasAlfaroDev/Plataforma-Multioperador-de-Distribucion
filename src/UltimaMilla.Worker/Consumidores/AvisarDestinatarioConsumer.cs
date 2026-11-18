using MassTransit;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Application.Notificaciones.Commands.AvisarDestinatario;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Worker.Consumidores;

/// <summary>
/// RF 27: correo al destinatario en los cambios de estado relevantes. Qué estados se avisan y el texto
/// los decide AvisarDestinatarioHandler. Tiene su propia cola: si el SMTP falla no frena los avisos a comercios.
/// </summary>
public sealed class AvisarDestinatarioConsumer(TenantDelMensaje tenant, AvisarDestinatarioHandler handler)
    : IConsumer<EstadoEnvioCambiado>
{
    public async Task Consume(ConsumeContext<EstadoEnvioCambiado> context)
    {
        var m = context.Message;

        // ADR-002: el operador viaja en el mensaje y se fija antes de tocar la base.
        tenant.Fijar(m.OperadorId);
        await handler.Handle(new AvisarDestinatarioCommand(m.EnvioId, m.EstadoNuevo), context.CancellationToken);
    }
}

public sealed class AvisarDestinatarioDefinition : ConsumerDefinition<AvisarDestinatarioConsumer>
{
    public AvisarDestinatarioDefinition()
    {
        EndpointName = "avisar-destinatario";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AvisarDestinatarioConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Si el SMTP no responde: 5 intentos con espera creciente y después a la cola avisar-destinatario_error.
        endpointConfigurator.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));

        // Inbox: si RabbitMQ reentrega un mensaje ya procesado, no se manda el correo dos veces.
        // El correo sale fuera de la transacción del inbox: si el SMTP lo acepta y después falla el commit,
        // la reentrega lo vuelve a mandar. Es un caso raro y un correo repetido no rompe nada.
        endpointConfigurator.UseEntityFrameworkOutbox<AppDbContext>(context);
    }
}
