using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Correo;

/// <summary>
/// SMTP de la biblioteca estándar. En el compose apunta a Mailpit, que captura los correos
/// y los muestra en mail.localhost:8000.
/// </summary>
/// <remarks>ponytail: sin reintentos propios; los hace la ConsumerDefinition de MassTransit.</remarks>
public sealed class SmtpEnviadorCorreo(IConfiguration configuration) : IEnviadorCorreo
{
    public async Task EnviarAsync(string para, string asunto, string cuerpoHtml, CancellationToken ct)
    {
        using var cliente = new SmtpClient(
            configuration["Correo:Host"] ?? "localhost",
            int.TryParse(configuration["Correo:Puerto"], out var puerto) ? puerto : 1025);

        using var mensaje = new MailMessage(
            configuration["Correo:Remitente"] ?? "seguimiento@ultimamilla.local",
            para,
            asunto,
            cuerpoHtml)
        {
            IsBodyHtml = true
        };

        await cliente.SendMailAsync(mensaje, ct);
    }
}
