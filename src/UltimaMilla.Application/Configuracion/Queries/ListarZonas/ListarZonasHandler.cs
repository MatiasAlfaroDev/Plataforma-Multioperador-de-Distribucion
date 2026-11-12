using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Configuracion.Queries.ListarZonas;

public sealed class ListarZonasHandler(
    IZonaCoberturaRepository zonas,
    ICuentaComercialRepository cuentas,
    ITenantContext tenant,
    IOperadorActivo operadorActivo)
{
    public async Task<IReadOnlyList<ZonaDto>> Handle(
        ListarZonasQuery query,
        CancellationToken ct)
    {
        if (!tenant.EstaAutenticado)
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión.");

        if (tenant.ComercioId.HasValue)
        {
            var cuenta = await cuentas.ObtenerPorOperadorYComercioAsync(
                query.OperadorId,
                tenant.ComercioId.Value,
                ct);

            if (cuenta is null ||
                cuenta.Estado != EstadoCuentaComercial.Confirmada ||
                !cuenta.Activa)
            {
                throw new UnauthorizedAccessException(
                    "No existe una vinculación activa con este operador.");
            }

            operadorActivo.SeleccionarOperador(query.OperadorId);
        }
        else if (tenant.OperadorId != query.OperadorId)
        {
            throw new UnauthorizedAccessException(
                "No tiene acceso a este operador.");
        }

        var lista = await zonas.ListarPorOperadorAsync(
            query.OperadorId,
            ct);

        
        return lista
            .Select(z => new ZonaDto(
                z.Id,
                z.Nombre,
                z.Departamento,
                z.Localidades
                    .Select(l => new LocalidadZonaDto(
                        l.Nombre,
                        l.Sector))
                    .ToList(),
                z.Franjas
                    .Select(f => new FranjaZonaDto(
                        f.HoraDesde,
                        f.HoraHasta))
                    .ToList()))
            .ToList();

    }
}