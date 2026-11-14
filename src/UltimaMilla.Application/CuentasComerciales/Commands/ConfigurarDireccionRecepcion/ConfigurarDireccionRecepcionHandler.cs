
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.CuentasComerciales.Commands.ConfigurarDireccionRecepcion;

public sealed class ConfigurarDireccionRecepcionHandler(
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    IOperadorActivo operadorActivo)
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

        // El usuario del comercio no tiene operador en la sesión: se fija el de la cuenta
        // (si es del comercio) para que el filtro por operador la devuelva y se pueda guardar.
        var vinculacion = (await cuentas.ListarPorComercioAsync(tenant.ComercioId.Value, ct))
            .FirstOrDefault(c => c.Id == command.CuentaComercialId)
            ?? throw new NoEncontradoException("La cuenta comercial no existe.");

        operadorActivo.SeleccionarOperador(vinculacion.OperadorId);

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
