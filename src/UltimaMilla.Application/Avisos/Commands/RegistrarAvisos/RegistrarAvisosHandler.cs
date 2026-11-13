using System.Text.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Eventos;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Envios;

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

            var cuerpo = CuerpoDelAviso(s.VersionContrato, command, envio);

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

    /// <summary>
    /// RF 36: el contrato se versiona por suscripción. La v1 no cambia (los comercios ya la consumen);
    /// la v2 agrega la versión, el estado de cada bulto y, en los avisos de bulto, el bulto que cambió.
    /// </summary>
    public static string CuerpoDelAviso(int versionContrato, RegistrarAvisosCommand command, Envio envio)
    {
        if (versionContrato < 2)
        {
            return JsonSerializer.Serialize(new
            {
                tipo = command.TipoEvento,
                envioId = envio.Id,
                referenciaExterna = envio.ReferenciaExterna,
                estado = envio.Estado.ToString(),
                ocurridoEn = command.OcurridoEn
            }, OpcionesJson);
        }

        return JsonSerializer.Serialize(new
        {
            version = 2,
            tipo = command.TipoEvento,
            envioId = envio.Id,
            referenciaExterna = envio.ReferenciaExterna,
            estado = envio.Estado.ToString(),
            ocurridoEn = command.OcurridoEn,
            bultos = new
            {
                total = envio.CantidadBultos,
                entregados = envio.BultosEntregados,
                detalle = envio.Bultos
                    .OrderBy(b => b.Identificacion)
                    .Select(b => new { identificacion = b.Identificacion, estado = b.Estado.ToString() })
            },
            bulto = command.Bulto is { } b
                ? new { identificacion = b.Identificacion, estadoAnterior = b.EstadoAnterior, estadoNuevo = b.EstadoNuevo, motivo = b.Motivo }
                : null
        }, OpcionesJson);
    }
}
