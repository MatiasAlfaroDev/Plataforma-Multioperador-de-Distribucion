using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;

/// <summary>
/// Caso de uso "sincronizar un evento de la app del repartidor" (RF 18, 21 y 24).
/// Reglas de la sincronización diferida (ADR de resolución de conflictos):
/// 1. Idempotencia: si el IdEvento ya llegó, responde 409 sin aplicarlo de nuevo; la app lo toma como éxito.
/// 2. El servidor manda: si el envío ya figura como entregado por otro evento, el nuevo se rechaza (400)
///    y la app lo conserva en su cola con el motivo, sin perderlo.
/// 3. Se guarda la hora real del teléfono (OcurridoEn) y la de llegada (RecibidoEn).
/// </summary>
/// <remarks>
/// Cuando exista la máquina de estados, acá se agrega la transición del envío
/// (envio.Entregar(...) / envio.RegistrarIntentoFallido(...)) antes de guardar.
/// </remarks>
public sealed class RegistrarEventoMovilHandler(
    IEnvioRepository envios,
    IEventoMovilRepository eventosMovil,
    IUnitOfWork unitOfWork,
    IPublicadorDeEventos eventos,
    IValidator<RegistrarEventoMovilCommand> validator,
    TimeProvider reloj)
{
    public async Task Handle(RegistrarEventoMovilCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        // 1. Idempotencia: un reintento del mismo evento no se aplica dos veces.
        if (await eventosMovil.ExisteAsync(command.IdEvento, ct))
            throw new ConflictoException("El evento ya fue registrado.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        // 2. Conflicto: otro evento ya dejó el envío como entregado. Gana lo que está en el servidor.
        var entregaPrevia = await eventosMovil.ObtenerEntregaAsync(envio.Id, ct);
        if (entregaPrevia is not null)
            throw new InvalidOperationException(
                $"Conflicto: el envío ya figura como entregado a {entregaPrevia.Receptor} " +
                $"({entregaPrevia.OcurridoEn:dd/MM/yyyy HH:mm} UTC).");

        var evento = EventoMovil.Registrar(
            command.IdEvento,
            envio,
            Enum.Parse<TipoEventoMovil>(command.Tipo, ignoreCase: true),
            command.Motivo,
            command.Receptor,
            command.FechaHora,
            reloj.GetUtcNow());

        await eventosMovil.AgregarAsync(evento, ct);

        // va antes de guardar porque se confirma en la misma transacción que el evento (outbox, RNF 6.8)
        await eventos.PublicarAsync(
            new EventoMovilRegistrado(evento.Id, evento.EnvioId, evento.OperadorId, evento.Tipo.ToString(), evento.OcurridoEn),
            ct);

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
