using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Envios.Queries.ListarEnvios;

public sealed class ListarEnviosHandler(
    IEnvioRepository envios,
    ICuentaComercialRepository cuentas,
    ITenantContext tenantContext,
    ICacheService cache)
{
    public async Task<IReadOnlyList<EnvioDto>> Handle(
        ListarEnviosQuery query,
        CancellationToken ct)
    {
        if (!tenantContext.EstaAutenticado ||
            !tenantContext.UsuarioId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión.");
        }

        var perfil = tenantContext.Perfil;

        var esOperador = Perfiles.PersonalOperador.Contains(perfil);

        var esComercio = perfil is
            Perfiles.AdministradorComercio or Perfiles.Comercio;

        if (!esOperador && !esComercio)
        {
            throw new UnauthorizedAccessException(
                "No tiene permisos para consultar envíos.");
        }

        if (esComercio)
        {
            var comercioId = tenantContext.ComercioId
                ?? throw new UnauthorizedAccessException(
                    "No se pudo identificar el comercio.");

            var vinculaciones = await cuentas.ListarPorComercioAsync(
                comercioId, ct);

            var autorizadas = vinculaciones
                .Where(c =>
                    c.Activa &&
                    c.Estado == EstadoCuentaComercial.Confirmada)
                .ToList();

            var cuentasIds = autorizadas
                .Select(c => c.Id)
                .ToArray();

            var lista = await envios.ListarPorCuentasComercialesAsync(
                cuentasIds, ct);

            var cuentasPorId = autorizadas.ToDictionary(c => c.Id);

            return lista.Select(e =>
            {
                cuentasPorId.TryGetValue(
                    e.CuentaComercialId,
                    out var cuenta);

                return new EnvioDto(
                    e.Id,
                    e.ReferenciaExterna,
                    e.OperadorId,
                    cuenta?.OperadorNombre ?? "(desconocido)",
                    cuenta?.ComercioNombre ?? "(desconocido)",
                    e.Modalidad.ToString(),
                    e.Estado.ToString(),
                    e.Destinatario.Nombre,
                    e.Direccion.ToString(),
                    e.Bultos.Count,
                    e.PesoTotalKg,
                    e.TarifaCalculada,
                    e.FechaAlta,
                    e.IntentosRealizados);
            }).ToList();
        }

        // Backoffice: se conserva el aislamiento por operador.
        var operadorId = tenantContext.OperadorId
            ?? throw new UnauthorizedAccessException(
                "Debe seleccionar un operador.");

        var claveCache = $"envios:operador:{operadorId}";

        var resultadoCache =
            await cache.ObtenerAsync<List<EnvioDto>>(claveCache, ct);

        if (resultadoCache is not null)
            return resultadoCache;

        var enviosOperador = await envios.ListarAsync(
            operadorId,
            null,
            ct);

        var cuentasPorOperador = (await cuentas.ListarResumenAsync(
            operadorId,
            null,
            ct))
            .ToDictionary(c => c.Id);

        var resultado = enviosOperador.Select(e =>
        {
            cuentasPorOperador.TryGetValue(
                e.CuentaComercialId,
                out var cuenta);

            return new EnvioDto(
                e.Id,
                e.ReferenciaExterna,
                e.OperadorId,
                cuenta?.OperadorNombre ?? "(desconocido)",
                cuenta?.ComercioNombre ?? "(desconocido)",
                e.Modalidad.ToString(),
                e.Estado.ToString(),
                e.Destinatario.Nombre,
                e.Direccion.ToString(),
                e.Bultos.Count,
                e.PesoTotalKg,
                e.TarifaCalculada,
                e.FechaAlta,
                e.IntentosRealizados);

        }).ToList();

        await cache.GuardarAsync(
            claveCache,
            resultado,
            TimeSpan.FromSeconds(30),
            ct);

        return resultado;
    }
}