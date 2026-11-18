
using System.Security.Cryptography;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;

namespace UltimaMilla.Application.Envios.Queries.ConsultarSeguimientoPublico;

public sealed class ConsultarSeguimientoPublicoHandler(
    IEnvioRepository envios,
    IOperadorRepository operadores,
    ICacheService cache)
{
    public async Task<SeguimientoPublicoDto?> Handle(
        string token,
        CancellationToken ct)
    {
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

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(tokenBytes));

        var clave = ClavesCache.Seguimiento(tokenHash);

        // Consultar Redis.
        var seguimientoCacheado =
            await cache.ObtenerAsync<SeguimientoPublicoDto>(
                clave, ct);

        if (seguimientoCacheado is not null)
        {
            // La identidad visual se obtiene actualizada.
            if (seguimientoCacheado.OperadorId is Guid operadorId)
            {
                var operadorActual =
                    await operadores.ObtenerAsync(operadorId, ct);

                if (operadorActual is null)
                    return null;

                return seguimientoCacheado with
                {
                    OperadorNombre = operadorActual.Nombre,
                    OperadorColorPrimario = operadorActual.ColorPrimario
                };
            }

            // Compatibilidad con entradas anteriores al cambio:
            // volver a construir el seguimiento.
            await cache.EliminarAsync(clave, ct);
        }

        var envio = await envios.ObtenerPorTokenSeguimientoAsync(
            tokenHash, ct);

        if (envio is null)
            return null;

        var operador = await operadores.ObtenerAsync(
            envio.OperadorId, ct);

        if (operador is null)
            return null;

        var historial = envio.Eventos
            .Select(e => new MovimientoSeguimientoDto(
                e.EstadoNuevo.ToString(),
                e.OcurridoEn))
            .Append(new MovimientoSeguimientoDto(
                "Admitido",
                envio.FechaAlta))
            .OrderBy(e => e.Fecha)
            .ToList();

        var seguimiento = new SeguimientoPublicoDto(
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
            envio.BultosEntregados,
            OperadorNombre: operador.Nombre,
            OperadorColorPrimario: operador.ColorPrimario,
            OperadorId: envio.OperadorId);

        await cache.GuardarAsync(
            clave,
            seguimiento,
            ClavesCache.DuracionSeguimiento,
            ct);

        return seguimiento;
    }
}
