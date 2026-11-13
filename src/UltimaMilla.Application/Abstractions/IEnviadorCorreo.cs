namespace UltimaMilla.Application.Abstractions;

/// <summary>Envío de correos a personas sin cuenta en la plataforma (el destinatario).</summary>
public interface IEnviadorCorreo
{
    Task EnviarAsync(string para, string asunto, string cuerpoHtml, CancellationToken ct);
}
