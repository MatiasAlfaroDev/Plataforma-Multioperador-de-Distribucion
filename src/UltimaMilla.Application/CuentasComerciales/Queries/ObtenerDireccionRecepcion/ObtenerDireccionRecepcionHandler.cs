
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Queries.ObtenerDireccionRecepcion;

public sealed class ObtenerDireccionRecepcionHandler(
    ICuentaComercialRepository cuentas,
    ITenantContext tenant)
{
    public async Task<DireccionRecepcionDto?> Handle(
        ObtenerDireccionRecepcionQuery query,
        CancellationToken ct)
    {
        if (!tenant.EstaAutenticado ||
            !tenant.ComercioId.HasValue ||
            tenant.Perfil is not (
                Perfiles.AdministradorComercio or Perfiles.Comercio))
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión como comercio.");
        }

        if (query.CuentaComercialId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta comercial es obligatoria.");

        var cuenta = await cuentas.ObtenerAsync(
            query.CuentaComercialId, ct);

        if (cuenta is null ||
            cuenta.ComercioId != tenant.ComercioId.Value)
        {
            throw new NoEncontradoException(
                "La cuenta comercial no existe.");
        }

        var direccion = cuenta.DireccionRecepcion;

        if (direccion is null)
            return null;

        return new DireccionRecepcionDto(
            direccion.Calle,
            direccion.Numero,
            direccion.Localidad,
            direccion.Departamento);
    }
}
