
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions.Caching;

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
            !tenantContext.UsuarioId.HasValue ||
            !tenantContext.OperadorId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión y seleccionar un operador.");
        }

        if (tenantContext.Perfil is not (
            Perfiles.AdministradorOperador or
            Perfiles.Operador or
            Perfiles.Comercio))
        {
            throw new UnauthorizedAccessException(
                "No tiene permisos para consultar envíos.");
        }

        var operadorId = tenantContext.OperadorId.Value;

        // El operador siempre se obtiene de la sesión,
        // nunca del parámetro enviado por el cliente.
        Guid? cuentaComercialId = null;

        if (tenantContext.Perfil == Perfiles.Comercio)
        {
            cuentaComercialId = tenantContext.ComercioId
                ?? throw new UnauthorizedAccessException(
                    "No tiene una cuenta comercial seleccionada.");
        }

        // La clave se construye con el contexto autenticado,
        // nunca con identificadores enviados por el cliente.
        var claveCache = cuentaComercialId.HasValue
            ? $"envios:operador:{operadorId}:cuenta:{cuentaComercialId.Value}"
            : $"envios:operador:{operadorId}";

        var resultadoCache = await cache.ObtenerAsync<List<EnvioDto>>(
            claveCache,
            ct);

        if (resultadoCache is not null)
        {
            return resultadoCache;
        }

        var lista = await envios.ListarAsync(
            operadorId,
            cuentaComercialId,
            ct);

        var cuentasPorId = (await cuentas.ListarResumenAsync(
            operadorId,
            cuentaComercialId,
            ct))
            .ToDictionary(c => c.Id);
        var resultado = lista
         .Select(e =>
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
                    e.FechaAlta);
            })
            .ToList();

        await cache.GuardarAsync(
            claveCache,
            resultado,
            TimeSpan.FromSeconds(30),
            ct);
        return resultado;
    }
}
