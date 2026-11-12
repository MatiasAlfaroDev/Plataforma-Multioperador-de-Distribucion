
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Envios.Commands.ConfirmarDevolucion;

public sealed class ConfirmarDevolucionHandler(
    IEnvioRepository envios,
    ICuentaComercialRepository cuentas,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IOperadorActivo operadorActivo,
    TimeProvider reloj,
    ICacheService cache,
    IHojaDeRutaRepository hojas)
{
    public async Task Handle(
        ConfirmarDevolucionCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CuentaComercialId == Guid.Empty ||
            command.EnvioId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta comercial y el envío son obligatorios.");

        if (string.IsNullOrWhiteSpace(command.Responsable) ||
            string.IsNullOrWhiteSpace(command.Receptor) ||
            string.IsNullOrWhiteSpace(command.ReferenciaConstancia))
            throw new ArgumentException(
                "Debe indicar responsable, receptor y constancia.");

        if (!Enum.TryParse<ModalidadDevolucion>(
                command.Modalidad,
                true,
                out var modalidad) ||
            !Enum.IsDefined(modalidad))
            throw new ArgumentException(
                "La modalidad de devolución no es válida.");

        if (command.IdentificacionesEscaneadas is null ||
            command.IdentificacionesEscaneadas.Count == 0)
            throw new ArgumentException(
                "Debe identificar los bultos recibidos.");

        if (!tenantContext.EstaAutenticado ||
            tenantContext.UsuarioId is null ||
            tenantContext.ComercioId is not Guid comercioId)
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión como comercio.");

        if (tenantContext.Perfil is not (
            Perfiles.Comercio or Perfiles.AdministradorComercio))
            throw new UnauthorizedAccessException(
                "No tiene permisos para confirmar devoluciones.");

        // Verificar primero la vinculación del comercio autenticado.
        var vinculaciones = await cuentas.ListarPorComercioAsync(
            comercioId, ct);

        var vinculacion = vinculaciones.FirstOrDefault(
            c => c.Id == command.CuentaComercialId);

        if (vinculacion is null ||
            !vinculacion.Activa ||
            vinculacion.Estado != EstadoCuentaComercial.Confirmada)
            throw new UnauthorizedAccessException(
                "La cuenta comercial no está vinculada al comercio.");

        // Habilitar el filtro del operador correspondiente.
        operadorActivo.SeleccionarOperador(vinculacion.OperadorId);

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (envio.CuentaComercialId != vinculacion.Id ||
            envio.OperadorId != vinculacion.OperadorId)
            throw new UnauthorizedAccessException(
                "El envío no pertenece a esta cuenta comercial.");

        if (envio.Estado != EstadoEnvio.EnDevolucion)
            throw new InvalidOperationException(
                "El envío no está en proceso de devolución.");

        var hojaActiva = await hojas.ObtenerPorEnvioActivoAsync(
            envio.OperadorId,
            envio.Id,
            ct);

        if (hojaActiva is not null)
        {
            var parada = hojaActiva.Paradas.SingleOrDefault(p =>
                p.Activa && p.EnvioId == envio.Id);

            if (parada?.TipoOperacion !=
                TipoOperacionRuta.DevolucionComercio)
            {
                throw new InvalidOperationException(
                    "El envío tiene una parada activa de otro tipo.");
            }

            if (hojaActiva.Estado != EstadoHojaDeRuta.Despachada)
            {
                throw new InvalidOperationException(
                    "La hoja de ruta todavía no fue despachada.");
            }

            if (modalidad != ModalidadDevolucion.EntregaDirecta)
            {
                throw new InvalidOperationException(
                    "La devolución en ruta debe confirmarse como entrega directa.");
            }
        }

        var esperados = envio.Bultos
            .Where(b => b.Estado == EstadoBulto.EnDevolucion)
            .Select(b => b.Identificacion)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var escaneados = command.IdentificacionesEscaneadas
            .Select(c => c?.Trim() ?? string.Empty)
            .ToList();

        if (escaneados.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Los códigos no pueden estar vacíos.");

        if (escaneados.Count !=
            escaneados.Distinct(
                StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidOperationException(
                "Hay códigos repetidos.");

        if (esperados.Count == 0 ||
            !esperados.SetEquals(
                escaneados.ToHashSet(
                    StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Los bultos recibidos no coinciden con los pendientes de devolución.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            envio.OperadorId, versionId, ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas.");

        // El responsable es el usuario autenticado.
        // No confiamos en el nombre enviado por el cliente.
        envio.Aplicar(
            DisparadorEnvio.ConfirmarDevolucion,
            reglas,
            reloj.GetUtcNow(),
            "PortalComercio",
            tenantContext.UsuarioId.Value.ToString(),
            modalidadDevolucion: modalidad,
            receptorDevolucion: command.Receptor.Trim(),
            referenciaConstanciaDevolucion:
                command.ReferenciaConstancia.Trim());
        
        if (hojaActiva is not null)
        {
            hojaActiva.FinalizarParada(
                envio,
                reloj.GetUtcNow());
        }

        await unitOfWork.GuardarCambiosAsync(ct);

        await cache.EliminarAsync(
            $"envios:operador:{envio.OperadorId}", ct);

        await cache.EliminarAsync(
            $"envios:operador:{envio.OperadorId}:cuenta:{envio.CuentaComercialId}",
            ct);
    }
}
