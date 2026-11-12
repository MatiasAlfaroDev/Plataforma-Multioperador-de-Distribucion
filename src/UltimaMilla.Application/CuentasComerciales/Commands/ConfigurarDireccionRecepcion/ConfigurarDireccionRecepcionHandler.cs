
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.CuentasComerciales.Commands.ConfigurarDireccionRecepcion;

public sealed class ConfigurarDireccionRecepcionHandler(
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant)
{
    public async Task Handle(
        ConfigurarDireccionRecepcionCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!tenant.EstaAutenticado ||
            !tenant.ComercioId.HasValue ||
            tenant.Perfil != Perfiles.AdministradorComercio)
        {
            throw new UnauthorizedAccessException(
                "Solo el administrador del comercio puede configurar la dirección.");
        }

        if (command.CuentaComercialId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta comercial es obligatoria.");

        var cuenta = await cuentas.ObtenerAsync(
            command.CuentaComercialId, ct);

        if (cuenta is null ||
            cuenta.ComercioId != tenant.ComercioId.Value)
        {
            throw new NoEncontradoException(
                "La cuenta comercial no existe.");
        }

        var direccion = new Direccion(
            command.Calle,
            command.Numero,
            command.Localidad,
            command.Departamento);

        cuenta.ConfigurarDireccionRecepcion(direccion);

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
