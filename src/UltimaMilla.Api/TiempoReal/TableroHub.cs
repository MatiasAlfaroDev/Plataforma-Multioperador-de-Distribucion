using MassTransit;
using Microsoft.AspNetCore.SignalR;
using UltimaMilla.Application.Envios.Eventos;

namespace UltimaMilla.Api.TiempoReal;

/// <summary>
/// Tablero de operación en vivo (RF 29, RNF 6.10). La misma cookie y los mismos perfiles que el resto de la API
/// (RequireAuthorization en Program.cs), y cada conexión entra solo al grupo de SU operador, tomado de la sesión:
/// el cliente no elige operador. El hub no guarda estado; avisa qué cambió y el Backoffice pide la foto de nuevo.
/// </summary>
public sealed class TableroHub : Hub
{
    public static string Grupo(Guid operadorId) => $"operador:{operadorId}";

    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst("operador_id")?.Value, out var operadorId))
        {
            // Sin operador elegido no hay tablero que mirar.
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(operadorId));
        await base.OnConnectedAsync();
    }
}

/// <summary>
/// Lleva al tablero cada cambio de estado de un envío, venga de la API o del Worker. Corre en la API: una sola
/// réplica toma cada mensaje de la cola y el backplane de Redis lo reparte a las conexiones de las dos.
/// </summary>
public sealed class TableroConsumer(IHubContext<TableroHub> hub) : IConsumer<EstadoEnvioCambiado>
{
    public Task Consume(ConsumeContext<EstadoEnvioCambiado> context) =>
        hub.Clients.Group(TableroHub.Grupo(context.Message.OperadorId)).SendAsync(
            "EnvioCambiado",
            new { context.Message.EnvioId, Estado = context.Message.EstadoNuevo.ToString() },
            context.CancellationToken);
}
