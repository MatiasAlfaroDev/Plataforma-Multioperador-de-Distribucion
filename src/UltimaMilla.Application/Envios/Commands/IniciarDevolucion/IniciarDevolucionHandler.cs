
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.IniciarDevolucion;

public sealed class IniciarDevolucionHandler(
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    TimeProvider reloj,
    ICacheService cache)
{
    public async Task Handle(
        IniciarDevolucionCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.EnvioId == Guid.Empty)
            throw new ArgumentException(
                "El envío es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException(
                "Debe identificarse al responsable.");

        if (!tenantContext.EstaAutenticado ||
            tenantContext.UsuarioId is null ||
            tenantContext.OperadorId is not Guid operadorId)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión y seleccionar un operador.");
        }

        if (tenantContext.Perfil is not (
            Perfiles.AdministradorOperador or
            Perfiles.Operador))
        {
            throw new UnauthorizedAccessException(
                "No tiene permisos para iniciar devoluciones.");
        }

        var envio = await envios.ObtenerAsync(
            command.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (envio.OperadorId != operadorId)
        {
            throw new UnauthorizedAccessException(
                "El envío pertenece a otro operador.");
        }

        if (envio.Estado != EstadoEnvio.EnDeposito)
        {
            throw new InvalidOperationException(
                "El envío debe estar en depósito para iniciar su devolución.");
        }

        if (envio.BultosPendientes == 0)
        {
            throw new InvalidOperationException(
                "No existen bultos pendientes para devolver.");
        }

        if (envio.VersionReglasOperativasId is not Guid versionId)
        {
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");
        }

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            operadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        envio.Aplicar(
            DisparadorEnvio.IniciarDevolucion,
            reglas,
            reloj.GetUtcNow(),
            "Backoffice",
            command.Responsable.Trim());

        await unitOfWork.GuardarCambiosAsync(ct);
        await cache.EliminarAsync(
            $"envios:operador:{envio.OperadorId}", ct);

        await cache.EliminarAsync(
            $"envios:operador:{envio.OperadorId}:cuenta:{envio.CuentaComercialId}",
            ct);
    }
}
