
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Correo;

/// <summary>
/// Envío de correos mediante SMTP autenticado.
/// Configurado para Gmail con STARTTLS.
/// </summary>
public sealed class SmtpEnviadorCorreo(IConfiguration configuration)
    : IEnviadorCorreo
{
    public async Task EnviarAsync(
        string para,
        string asunto,
        string cuerpoHtml,
        CancellationToken ct)
    {
        var host = configuration["Correo:Host"]
            ?? throw new InvalidOperationException(
                "No se configuró el servidor SMTP.");

        var puerto = int.TryParse(
            configuration["Correo:Puerto"], out var valor)
            ? valor
            : 587;

        var usuario = configuration["Correo:Usuario"]
            ?? throw new InvalidOperationException(
                "No se configuró el usuario SMTP.");

        var password = configuration["Correo:Password"]
            ?? throw new InvalidOperationException(
                "No se configuró la contraseña SMTP.");

        var remitente = configuration["Correo:Remitente"]
            ?? usuario;

        // Correo:Ssl=false para Mailpit en local (sin TLS ni credenciales).
        var ssl = configuration["Correo:Ssl"] != "false";

        using var cliente = new SmtpClient(host, puerto)
        {
            EnableSsl = ssl,
            UseDefaultCredentials = false,
            Credentials = ssl ? new NetworkCredential(usuario, password) : null,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };

        using var mensaje = new MailMessage(
            remitente,
            para,
            asunto,
            cuerpoHtml)
        {
            IsBodyHtml = true
        };

        await cliente.SendMailAsync(mensaje, ct);
    }
}
