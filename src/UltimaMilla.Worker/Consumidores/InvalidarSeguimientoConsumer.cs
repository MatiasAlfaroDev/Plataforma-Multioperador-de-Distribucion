using MassTransit;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Application.Envios.Eventos;

namespace UltimaMilla.Worker.Consumidores;

/// <summary>
/// Aclaración 3.1: la entrada del seguimiento público se invalida cuando se procesa un evento del envío,
/// así el destinatario ve el cambio sin esperar a que venza. Tiene su propia cola: recibe una copia de
/// cada evento, independiente de la que genera los avisos.
/// </summary>
public sealed class InvalidarSeguimientoConsumer(TenantDelMensaje tenant, IEnvioRepository envios, ICacheService cache)
    : IConsumer<EstadoEnvioCambiado>, IConsumer<EstadoBultoCambiado>
{
    public Task Consume(ConsumeContext<EstadoEnvioCambiado> context) =>
        InvalidarAsync(context.Message.EnvioId, context.Message.OperadorId, context.CancellationToken);

    public Task Consume(ConsumeContext<EstadoBultoCambiado> context) =>
        InvalidarAsync(context.Message.EnvioId, context.Message.OperadorId, context.CancellationToken);

    private async Task InvalidarAsync(Guid envioId, Guid operadorId, CancellationToken ct)
    {
        tenant.Fijar(operadorId);

        if (await envios.ObtenerAsync(envioId, ct) is { TokenSeguimientoHash: { } hash })
            await cache.EliminarAsync(ClavesCache.Seguimiento(hash), ct);
    }
}

public sealed class InvalidarSeguimientoDefinition : ConsumerDefinition<InvalidarSeguimientoConsumer>
{
    public InvalidarSeguimientoDefinition()
    {
        EndpointName = "invalidar-cache-seguimiento";
    }
}
