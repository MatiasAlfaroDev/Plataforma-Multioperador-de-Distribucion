using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.Envios.Commands.RetornarADeposito;

public sealed class RetornarADepositoHandler(
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IVersionTarifariaRepository tarifas,
    IUnitOfWork unitOfWork,
    TimeProvider reloj,
    ITenantContext tenantContext)
{
    public async Task Handle(
        RetornarADepositoCommand command,
        CancellationToken ct)
    {
        if (command.EnvioId == Guid.Empty)
            throw new ArgumentException("El envío es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException("Debe identificarse al responsable.");

        if (command.IdentificacionesEscaneadas is null ||
            command.IdentificacionesEscaneadas.Count == 0)
            throw new ArgumentException("Debe escanearse al menos un bulto.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        if (envio.OperadorId != tenantContext.OperadorId)
        {
            throw new UnauthorizedAccessException(
                "No tiene permisos para modificar envíos de otro operador.");
        }

        if (envio.Estado != EstadoEnvio.NoEntregado &&
            envio.Estado != EstadoEnvio.EntregadoParcialmente)
        {
            throw new InvalidOperationException(
                "Solo pueden retornar al depósito envíos no entregados o parcialmente entregados.");
        }

        var esperados = envio.Bultos
            .Where(b => b.Estado == EstadoBulto.Pendiente)
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
                        
        if (!tenantContext.EstaAutenticado ||
            !tenantContext.UsuarioId.HasValue ||
            !tenantContext.OperadorId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión y seleccionar un operador.");
        }

        if (!Perfiles.Deposito.Contains(tenantContext.Perfil))
        {
            throw new UnauthorizedAccessException(
                "No tiene permisos para registrar retornos al depósito.");
        }

        var recibidos = escaneados
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!esperados.SetEquals(recibidos))
            throw new InvalidOperationException(
                "Los bultos escaneados no coinciden con los declarados en el envío.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene una versión de reglas operativas asignada.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            envio.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        var ahora = reloj.GetUtcNow();
        var veniaDeEntregaParcial = envio.Estado == EstadoEnvio.EntregadoParcialmente;

        envio.Aplicar(
            DisparadorEnvio.RetornarADeposito,
            reglas,
            ahora,
            "Deposito",
            command.Responsable.Trim());

        // RF 33 y 34: los bultos pendientes de una entrega parcial siguen lo que digan las reglas del operador,
        // sin esperar una decisión manual. Una nueva visita los deja Reprogramado, listos para otra hoja de ruta.
        if (veniaDeEntregaParcial)
        {
            if (reglas.AccionBultosPendientes == AccionBultosPendientes.NuevaVisita && envio.AdmiteNuevaVisita(reglas))
            {
                envio.Aplicar(DisparadorEnvio.Reprogramar, reglas, ahora, "Sistema", "Reglas operativas");

                if (reglas.NuevaVisitaConRecargo)
                {
                    // ponytail: el recargo es el precio base vigente de la zona; un importe propio en las reglas si hace falta.
                    var tarifa = await tarifas.ObtenerVigenteAsync(envio.OperadorId, envio.Direccion.Departamento, ahora, ct);
                    envio.AgregarRecargoNuevaVisita(tarifa?.PrecioBase ?? 0);
                }
            }
            else if (envio.PuedeIniciarDevolucion(reglas))
            {
                envio.Aplicar(DisparadorEnvio.IniciarDevolucion, reglas, ahora, "Sistema", "Reglas operativas");
            }
        }

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
