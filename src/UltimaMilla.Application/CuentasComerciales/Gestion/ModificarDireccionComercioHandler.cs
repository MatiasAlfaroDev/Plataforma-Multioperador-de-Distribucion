using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.CuentasComerciales.Gestion;

/// <summary>Modificación, por el operador, de la dirección de recepción de un comercio.</summary>
public sealed class ModificarDireccionComercioHandler(
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant)
{
    public async Task Handle(
        ModificarDireccionComercioCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!tenant.EstaAutenticado || !tenant.OperadorId.HasValue)
            throw new UnauthorizedAccessException("Debe iniciar sesión como operador.");

        if (tenant.Perfil != Perfiles.AdministradorOperador)
            throw new UnauthorizedAccessException(
                "Solo el administrador del operador puede modificar comercios.");

        var cuenta = await cuentas.ObtenerAsync(command.CuentaId, ct);

        if (cuenta is null || cuenta.OperadorId != tenant.OperadorId.Value)
            throw new KeyNotFoundException("El comercio no está vinculado a este operador.");

        // El value object Direccion valida los campos obligatorios (ArgumentException).
        cuenta.ConfigurarDireccionRecepcion(new Direccion(
            command.Calle,
            command.Numero,
            command.Localidad,
            command.Departamento));

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
