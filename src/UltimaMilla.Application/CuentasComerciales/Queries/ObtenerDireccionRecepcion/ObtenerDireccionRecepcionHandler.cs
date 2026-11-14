
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Queries.ObtenerDireccionRecepcion;

public sealed class ObtenerDireccionRecepcionHandler(
    ICuentaComercialRepository cuentas,
    ITenantContext tenant,
    IOperadorActivo operadorActivo)
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

        // El usuario del comercio no tiene operador en la sesión: se fija el de la cuenta
        // (si es del comercio) para que el filtro por operador la devuelva y se pueda guardar.
        var vinculacion = (await cuentas.ListarPorComercioAsync(tenant.ComercioId.Value, ct))
            .FirstOrDefault(c => c.Id == query.CuentaComercialId)
            ?? throw new NoEncontradoException("La cuenta comercial no existe.");

        operadorActivo.SeleccionarOperador(vinculacion.OperadorId);

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
