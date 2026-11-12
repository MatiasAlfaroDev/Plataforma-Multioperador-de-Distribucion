using System.Security.Cryptography;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Envios.Queries.ConsultarSeguimientoPublico;

public sealed class ConsultarSeguimientoPublicoHandler(
    IEnvioRepository envios)
{
    public async Task<SeguimientoPublicoDto?> Handle(
        string token,
        CancellationToken ct)
    {
        // El token debe ser hexadecimal y tener 64 caracteres.
        if (string.IsNullOrWhiteSpace(token) ||
            token.Length != 64)
        {
            return null;
        }

        byte[] tokenBytes;

        try
        {
            tokenBytes = Convert.FromHexString(token);
        }
        catch (FormatException)
        {
            return null;
        }

        // Calculamos el mismo hash utilizado al crear el envío.
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(tokenBytes));

        var envio = await envios.ObtenerPorTokenSeguimientoAsync(
            tokenHash, ct);

        if (envio is null)
        {
            return null;
        }

        var historial = envio.Eventos
            .Select(e => new MovimientoSeguimientoDto(
                e.EstadoNuevo.ToString(),
                e.OcurridoEn))
            .Append(new MovimientoSeguimientoDto(
                "Admitido",
                envio.FechaAlta))
            .OrderBy(e => e.Fecha)
            .ToList();

        return new SeguimientoPublicoDto(
            envio.ReferenciaExterna,
            envio.Estado.ToString(),
            envio.FechaAlta,
            envio.FechaEntregaComprometida,
            envio.HoraEntregaDesde,
            envio.HoraEntregaHasta,
            envio.FechaReprogramada,
            envio.FranjaReprogramada,
            historial,
            envio.CantidadBultos,
            envio.BultosEntregados);
    }
}