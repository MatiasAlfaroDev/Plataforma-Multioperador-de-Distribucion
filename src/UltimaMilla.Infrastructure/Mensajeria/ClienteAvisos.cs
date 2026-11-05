using System.Text;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos;

namespace UltimaMilla.Infrastructure.Mensajeria;

/// <summary>
/// POST JSON firmado al sistema del comercio (RNF 6.9). Sin Polly: los reintentos con espera
/// creciente los hace MassTransit en el Worker; acá hay un solo intento con el Timeout del HttpClient.
/// </summary>
public sealed class ClienteAvisos(HttpClient http) : IClienteAvisos
{
    /// <summary>
    /// Envía un aviso al sistema del comercio con firma HMAC-SHA256.
    /// </summary>
    /// <exception cref="HttpRequestException">Si el destino no responde con código 2xx.</exception>
    public async Task EnviarAsync(string url, string secreto, Guid entregaId, string tipoEvento, string cuerpo, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };

        pedido.Headers.Add(FirmaAviso.Cabecera, FirmaAviso.Calcular(secreto, cuerpo));
        pedido.Headers.Add("X-UltimaMilla-Entrega", entregaId.ToString()); // el receptor deduplica con esto: un reintento llega con el mismo id
        pedido.Headers.Add("X-UltimaMilla-Evento", tipoEvento);

        try
        {
            using var respuesta = await http.SendAsync(pedido, ct);
            if (!respuesta.IsSuccessStatusCode)
                throw new HttpRequestException($"El receptor respondió HTTP {(int)respuesta.StatusCode}.", null, respuesta.StatusCode);

            AvisosMetrics.RegistrarEntregado();
        }
        catch
        {
            AvisosMetrics.RegistrarFallido();
            throw;
        }
    }
}
