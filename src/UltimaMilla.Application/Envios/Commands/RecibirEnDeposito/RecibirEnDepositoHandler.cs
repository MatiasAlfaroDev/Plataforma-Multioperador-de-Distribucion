
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;

public sealed class RecibirEnDepositoHandler(
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj,
    ICacheService cache,
    ITenantContext tenantContext)
{
    public async Task Handle(
        RecibirEnDepositoCommand command,
        CancellationToken ct)
    {
        // 1. Validar los datos recibidos.
        if (command.EnvioId == Guid.Empty)
            throw new ArgumentException("El envío es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException("Debe identificarse al responsable.");

        if (command.IdentificacionesEscaneadas is null ||
            command.IdentificacionesEscaneadas.Count == 0)
            throw new ArgumentException("Debe escanearse al menos un bulto.");

        // 2. Validar autenticación y operador seleccionado.
        if (!tenantContext.EstaAutenticado ||
            !tenantContext.UsuarioId.HasValue ||
            !tenantContext.OperadorId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión y seleccionar un operador.");
        }

        // 3. Validar los permisos del usuario.
        if (tenantContext.Perfil is not (
            Perfiles.AdministradorOperador or
            Perfiles.Operador))
        {
            throw new UnauthorizedAccessException(
                "No tiene permisos para recibir envíos en depósito.");
        }

        // 4. Obtener el envío.
        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        // 5. Verificar que pertenece al operador autenticado.
        if (envio.OperadorId != tenantContext.OperadorId.Value)
        {
            throw new UnauthorizedAccessException(
                "No tiene permisos para modificar envíos de otro operador.");
        }

        // 6. Validar el estado actual.
        if (envio.Estado != EstadoEnvio.Admitido)
            throw new InvalidOperationException(
                "Solo se pueden recibir envíos en estado Admitido.");

        // 7. Validar las identificaciones de los bultos.
        var esperados = envio.Bultos
            .Select(b => b.Identificacion)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var escaneados = command.IdentificacionesEscaneadas
            .Select(codigo => codigo?.Trim() ?? string.Empty)
            .ToList();

        if (escaneados.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Las identificaciones escaneadas no pueden estar vacías.");

        if (escaneados.Count != escaneados
                .Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidOperationException(
                "Hay identificaciones escaneadas repetidas.");

        var recibidos = escaneados
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!esperados.SetEquals(recibidos))
            throw new InvalidOperationException(
                "Los bultos escaneados no coinciden con los declarados en el envío.");

        // 8. Obtener las reglas operativas.
        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene una versión de reglas operativas asignada.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            envio.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        // 9. Aplicar el cambio de estado.
        envio.Aplicar(
            DisparadorEnvio.RecibirEnDeposito,
            reglas,
            reloj.GetUtcNow(),
            "Backoffice",
            command.Responsable.Trim());

        // 10. Guardar los cambios en PostgreSQL.
        await unitOfWork.GuardarCambiosAsync(ct);

        // 11. Invalidar la caché del operador.
        var claveOperador =
            $"envios:operador:{envio.OperadorId}";

        await cache.EliminarAsync(claveOperador, ct);

        // 12. Invalidar la caché del comercio.
        var claveComercio =
            $"envios:operador:{envio.OperadorId}:cuenta:{envio.CuentaComercialId}";

        await cache.EliminarAsync(claveComercio, ct);
    }
}
