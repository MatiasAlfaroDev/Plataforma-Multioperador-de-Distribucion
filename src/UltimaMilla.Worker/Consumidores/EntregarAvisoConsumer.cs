using MassTransit;
using UltimaMilla.Application.Avisos.Commands.EntregarAviso;
using UltimaMilla.Application.Avisos.Eventos;

namespace UltimaMilla.Worker.Consumidores;

/// <summary>
/// Consumer que procesa avisos pendientes y los entrega al comercio.
/// </summary>
public sealed class EntregarAvisoConsumer(TenantDelMensaje tenant, EntregarAvisoHandler handler) : IConsumer<AvisoPendiente>
{
    public async Task Consume(ConsumeContext<AvisoPendiente> context)
    {
        tenant.Fijar(context.Message.OperadorId);
        await handler.Handle(new EntregarAvisoCommand(context.Message.EntregaAvisoId), context.CancellationToken);
    }
}

public sealed class EntregarAvisoDefinition : ConsumerDefinition<EntregarAvisoConsumer>
{
    public EntregarAvisoDefinition()
    {
        EndpointName = "entregar-aviso";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<EntregarAvisoConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Reintentos con espera creciente: 5 intentos entre 2 s y 1 min; agotados, el aviso queda Fallida y el mensaje en entregar-aviso_error.
        endpointConfigurator.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));

        // Sin outbox a propósito: el Handler guarda el intento fallido antes de relanzar y con la transacción del outbox se perdería.
    }
}
