using MassTransit;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Application.Notificaciones.Commands.AvisarAsignacion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Worker.Consumidores;

/// <summary>
/// Push al repartidor cuando un envío pasa a "Asignado a ruta" (reasignación urgente).
/// Escucha el mismo evento que los avisos a comercios, pero en su propia cola: si Firebase falla,
/// los avisos a los comercios no se frenan, y al revés.
/// </summary>
public sealed class AvisarAsignacionConsumer(TenantDelMensaje tenant, AvisarAsignacionHandler handler)
    : IConsumer<EstadoEnvioCambiado>
{
    public async Task Consume(ConsumeContext<EstadoEnvioCambiado> context)
    {
        var m = context.Message;
        if (m.EstadoNuevo != EstadoEnvio.AsignadoARuta)
            return;

        // ADR-002: el operador viaja en el mensaje y se fija antes de tocar la base.
        tenant.Fijar(m.OperadorId);
        await handler.Handle(new AvisarAsignacionCommand(m.EnvioId, m.OcurridoEn), context.CancellationToken);
    }
}

public sealed class AvisarAsignacionDefinition : ConsumerDefinition<AvisarAsignacionConsumer>
{
    public AvisarAsignacionDefinition()
    {
        EndpointName = "avisar-asignacion";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AvisarAsignacionConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Pocos reintentos y cortos: el aviso es urgente, de nada sirve que llegue una hora después.
        endpointConfigurator.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)));

        // Inbox: si RabbitMQ reentrega el mismo mensaje ya procesado, no se manda el push dos veces.
        endpointConfigurator.UseEntityFrameworkOutbox<AppDbContext>(context);
    }
}
