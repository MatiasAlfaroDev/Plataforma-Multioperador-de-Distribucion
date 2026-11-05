namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Cliente HTTP para enviar avisos a los sistemas de comercios.
/// </summary>
public interface IClienteAvisos
{
    /// <summary>
    /// Envía un aviso al sistema del comercio con firma HMAC-SHA256.
    /// </summary>
    /// <exception cref="HttpRequestException">Si el destino no responde con código 2xx.</exception>
    Task EnviarAsync(string url, string secreto, Guid entregaId, string tipoEvento, string cuerpo, CancellationToken ct);
}
