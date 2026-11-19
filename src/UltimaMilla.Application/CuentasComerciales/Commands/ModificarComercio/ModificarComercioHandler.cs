using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Commands.ModificarComercio;

/// <summary>
/// El comercio es global (lo comparten todos sus operadores), así que solo lo modifica su propio
/// administrador: un operador no puede escribir datos que ven los demás operadores (ADR-002).
/// </summary>
public sealed class ModificarComercioHandler(
    IComercioRepository comercios,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    IValidator<ModificarComercioCommand> validator)
{
    public async Task Handle(ModificarComercioCommand command, CancellationToken ct)
    {
        if (!tenant.EstaAutenticado ||
            tenant.ComercioId is not Guid comercioId ||
            tenant.Perfil != Perfiles.AdministradorComercio)
            throw new UnauthorizedAccessException(
                "Solo el administrador del comercio puede modificar sus datos.");

        await validator.ValidateAndThrowAsync(command, ct);

        var comercio = await comercios.ObtenerAsync(comercioId, ct)
            ?? throw new NoEncontradoException("El comercio no existe.");

        comercio.Modificar(command.Nombre);

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
