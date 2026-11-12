using System.Text.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Eventos;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.RegistrarAvisos;

/// <summary>
/// Crea un aviso por cada suscripción de la cuenta que escucha el tipo.
/// Es idempotente: si el evento ya generó el aviso de una suscripción, no lo repite.
/// </summary>
public sealed class RegistrarAvisosHandler(
    IEnvioRepository envios,
    ISuscripcionAvisoRepository suscripciones,
    IEntregaAvisoRepository entregas,
    IPublicadorDeEventos eventos,
    IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<int> Handle(RegistrarAvisosCommand command, CancellationToken ct)
    {
        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        var lista = await suscripciones.ListarAsync([envio.CuentaComercialId], ct);
        int creados = 0;

        foreach (var s in lista.Where(s => s.Escucha(command.TipoEvento)))
        {
            if (await entregas.ExisteAsync(s.Id, command.ClaveEvento, ct))
                continue;

            var cuerpo = JsonSerializer.Serialize(new
            {
                tipo = command.TipoEvento,
                envioId = envio.Id,
                referenciaExterna = envio.ReferenciaExterna,
                estado = envio.Estado.ToString(),
                ocurridoEn = command.OcurridoEn
            }, OpcionesJson);

            var entrega = EntregaAviso.Crear(s, command.TipoEvento, command.ClaveEvento, cuerpo);
            await entregas.AgregarAsync(entrega, ct);

            // Antes de guardar: el outbox lo confirma en la misma transacción.
            await eventos.PublicarAsync(new AvisoPendiente(entrega.Id, entrega.OperadorId), ct);
            creados++;
        }

        if (creados > 0)
            await unitOfWork.GuardarCambiosAsync(ct);

        return creados;
    }
}
