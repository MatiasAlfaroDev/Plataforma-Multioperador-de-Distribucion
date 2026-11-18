using System.Net;
using System.Text.RegularExpressions;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Notificaciones.Commands.AvisarDestinatario;

/// <param name="EnvioId">El envío que cambió de estado.</param>
/// <param name="EstadoNuevo">El estado al que pasó (viene en EstadoEnvioCambiado).</param>
public sealed record AvisarDestinatarioCommand(Guid EnvioId, EstadoEnvio EstadoNuevo);

/// <summary>
/// RF 27: correo al destinatario en los cambios de estado relevantes, con la marca del operador (RF 5).
/// Lo llama el Worker al consumir EstadoEnvioCambiado, con el operador del mensaje ya fijado en el contexto.
/// Devuelve si mandó el correo.
/// </summary>
/// <remarks>
/// El correo no lleva el enlace de seguimiento: el envío solo guarda el hash del token (el token en claro
/// existe una única vez, en EnlaceSeguimientoSolicitado), así que acá no hay forma de volver a armarlo.
/// Se remite al enlace del primer correo. Para incluirlo habría que guardar el token cifrado en el envío.
/// </remarks>
public sealed partial class AvisarDestinatarioHandler(
    IEnvioRepository envios,
    IOperadorRepository operadores,
    IEnviadorCorreo correo)
{
    private const string ColorPorDefecto = "#1f2937";

    /// <summary>Estados que se le avisan al destinatario; el resto son internos del operador.</summary>
    public static readonly IReadOnlyDictionary<EstadoEnvio, (string Asunto, string Texto)> Mensajes =
        new Dictionary<EstadoEnvio, (string, string)>
        {
            [EstadoEnvio.EnTransito] = ("está en camino", "salió a reparto y está en camino."),
            [EstadoEnvio.Entregado] = ("fue entregado", "fue entregado."),
            [EstadoEnvio.EntregadoParcialmente] = ("fue entregado en parte", "fue entregado en parte; el resto sigue en gestión."),
            [EstadoEnvio.NoEntregado] = ("no pudo entregarse", "no pudo entregarse en esta visita."),
            [EstadoEnvio.Reprogramado] = ("fue reprogramado", "tiene una nueva fecha de entrega.")
        };

    public async Task<bool> Handle(AvisarDestinatarioCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Mensajes.TryGetValue(command.EstadoNuevo, out var mensaje))
            return false;

        if (await envios.ObtenerAsync(command.EnvioId, ct) is not { Destinatario.Email: { } email } envio)
            return false;

        var operador = await operadores.ObtenerAsync(envio.OperadorId, ct);
        var marca = operador?.Nombre ?? "Tu operador logístico";

        // El color va dentro de un atributo style: solo se acepta un hexadecimal.
        var color = operador is not null && ColorHex().IsMatch(operador.ColorPrimario) ? operador.ColorPrimario : ColorPorDefecto;

        var referencia = WebUtility.HtmlEncode(envio.ReferenciaExterna);
        var cuerpo =
            $"<div style=\"border-top:4px solid {color};padding:16px;font-family:sans-serif\">" +
            $"<h2 style=\"color:{color};margin:0 0 12px\">{WebUtility.HtmlEncode(marca)}</h2>" +
            $"<p>Hola {WebUtility.HtmlEncode(envio.Destinatario.Nombre)}:</p>" +
            $"<p>Tu pedido <strong>{referencia}</strong> {mensaje.Texto}</p>" +
            "<p>Podés ver el detalle en el enlace de seguimiento que te enviamos cuando se creó el envío.</p>" +
            "</div>";

        await correo.EnviarAsync(email, $"{marca}: tu pedido {envio.ReferenciaExterna} {mensaje.Asunto}", cuerpo, ct);
        return true;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{3,8}$")]
    private static partial Regex ColorHex();
}
