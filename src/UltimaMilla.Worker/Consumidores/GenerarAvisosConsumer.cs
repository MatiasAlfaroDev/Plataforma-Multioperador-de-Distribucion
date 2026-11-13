using MassTransit;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisos;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Worker.Consumidores;

/// <summary>
/// Consumer que convierte eventos de envío en avisos a comercios suscriptos (RF 28).
/// Es idempotente: usa el inbox de MassTransit y un índice único en entregas_aviso.
/// </summary>
public sealed class GenerarAvisosConsumer(TenantDelMensaje tenant, RegistrarAvisosHandler handler, IBanderas banderas)
    : IConsumer<EnvioCreado>, IConsumer<EstadoEnvioCambiado>, IConsumer<EstadoBultoCambiado>
{
    // RF 36: avisos por bulto; RegistrarAvisos solo los genera para suscripciones con contrato v2.
    public async Task Consume(ConsumeContext<EstadoBultoCambiado> context)
    {
        var m = context.Message;
        var tipo = TiposAviso.DeResultadoBulto(m.Resultado);

        // Aclaración 3.2: sin la funcionalidad de entrega parcial el operador no avisa por bulto.
        if (!await banderas.EstaHabilitadaAsync(Banderas.EntregaParcial, m.OperadorId, context.CancellationToken))
            return;

        tenant.Fijar(m.OperadorId);
        await handler.Handle(
            new RegistrarAvisosCommand(m.EnvioId, tipo, $"{tipo}:{m.EventoId}", m.OcurridoEn,
                new BultoDelAviso(m.Identificacion, m.EstadoAnterior.ToString(), m.EstadoNuevo.ToString(), m.Motivo)),
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<EnvioCreado> context)
    {
        var m = context.Message;
        tenant.Fijar(m.OperadorId);
        await handler.Handle(new RegistrarAvisosCommand(m.EnvioId, TiposAviso.EnvioCreado, $"{TiposAviso.EnvioCreado}:{m.EnvioId}", m.FechaAlta), context.CancellationToken);
    }

    // Cubre todas las transiciones, también las de la app (EventoMovilRegistrado ya no genera avisos: serían dobles).
    public async Task Consume(ConsumeContext<EstadoEnvioCambiado> context)
    {
        var m = context.Message;
        var tipo = TiposAviso.DeEstado(m.EstadoNuevo);

        if (tipo is null)
            return;

        tenant.Fijar(m.OperadorId);
        await handler.Handle(new RegistrarAvisosCommand(m.EnvioId, tipo, $"{tipo}:{m.EventoId}", m.OcurridoEn), context.CancellationToken);
    }
}

public sealed class GenerarAvisosDefinition : ConsumerDefinition<GenerarAvisosConsumer>
{
    public GenerarAvisosDefinition()
    {
        EndpointName = "generar-avisos";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<GenerarAvisosConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Reintentos con espera creciente (RNF 6.8): 5 intentos entre 2 s y 1 min; agotados, el mensaje pasa a la cola generar-avisos_error.
        endpointConfigurator.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));

        // Inbox + outbox: el mensaje se procesa una sola vez y los AvisoPendiente se publican en la misma transacción que las entregas.
        endpointConfigurator.UseEntityFrameworkOutbox<AppDbContext>(context);
    }
}
