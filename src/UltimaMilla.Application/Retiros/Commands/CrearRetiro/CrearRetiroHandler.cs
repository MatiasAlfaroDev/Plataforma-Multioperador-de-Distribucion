using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Retiros.Commands.CrearRetiro;

public sealed class CrearRetiroHandler(
    IRetiroRepository retiros,
    ICuentaComercialRepository cuentas,
    IZonaCoberturaRepository zonas,
    IVersionTarifariaRepository tarifas,
    IUnitOfWork unitOfWork,
    IValidator<CrearRetiroCommand> validator,
    ITenantContext tenantContext,
    IOperadorActivo operadorActivo,
    TimeProvider reloj,
    RegistrarAvisosRetiroHandler avisos)
{
    public async Task<Guid> Handle(
        CrearRetiroCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!tenantContext.EstaAutenticado ||
            !tenantContext.UsuarioId.HasValue ||
            !tenantContext.ComercioId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión como comercio para solicitar un retiro.");
        }

        if (tenantContext.Perfil is not (
            Perfiles.AdministradorComercio or Perfiles.Comercio))
        {
            throw new UnauthorizedAccessException(
                "Su perfil no permite solicitar retiros.");
        }

        await validator.ValidateAndThrowAsync(command, ct);

        var vinculaciones = await cuentas.ListarPorComercioAsync(
            tenantContext.ComercioId.Value, ct);

        var vinculacion = vinculaciones.FirstOrDefault(
            c => c.Id == command.CuentaComercialId);

        if (vinculacion is null ||
            !vinculacion.Activa ||
            vinculacion.Estado != EstadoCuentaComercial.Confirmada)
        {
            throw new UnauthorizedAccessException(
                "No tiene una cuenta comercial activa y confirmada.");
        }

        operadorActivo.SeleccionarOperador(vinculacion.OperadorId);

        var cuenta = await cuentas.ObtenerAsync(
            command.CuentaComercialId, ct)
            ?? throw new NoEncontradoException(
                "La cuenta comercial no existe.");

        if (cuenta.ComercioId != tenantContext.ComercioId.Value ||
            cuenta.OperadorId != vinculacion.OperadorId)
        {
            throw new UnauthorizedAccessException(
                "La cuenta comercial no pertenece al comercio autenticado.");
        }

        // RF37: validar cobertura y franja del retiro solicitado.
        // La cuenta comercial y el operador activo ya fueron verificados.

        var zonasOperador = await zonas.ListarPorOperadorAsync(
            cuenta.OperadorId,
            ct);

        var zonasCompatibles = zonasOperador
            .Where(z =>
                z.Activa &&
                string.Equals(
                    z.Departamento,
                    command.DomicilioRetiro.Departamento.Trim(),
                    StringComparison.OrdinalIgnoreCase) &&
                z.CubreDireccion(
                    command.DomicilioRetiro.Localidad,
                    null))
            .ToList();

        if (zonasCompatibles.Count == 0)
        {
            throw new InvalidOperationException(
                "El domicilio de retiro no está dentro de la cobertura del operador.");
        }

        if (zonasCompatibles.Count > 1)
        {
            throw new InvalidOperationException(
                "El domicilio de retiro coincide con varias zonas de cobertura.");
        }

        var zona = zonasCompatibles[0];

        if (!zona.OfreceFranja(
            command.HoraDesde,
            command.HoraHasta))
        {
            throw new InvalidOperationException(
                "La franja solicitada no está disponible para esta zona.");
        }

        var retiro = Retiro.Crear(
            cuenta.OperadorId,
            cuenta.Id,
            command.Motivo,
            new Direccion(
                command.DomicilioRetiro.Calle,
                command.DomicilioRetiro.Numero,
                command.DomicilioRetiro.Localidad,
                command.DomicilioRetiro.Departamento),
            command.DomicilioDestino is { } destino
                ? new Direccion(destino.Calle, destino.Numero, destino.Localidad, destino.Departamento)
                : cuenta.DireccionRecepcion
                    ?? throw new InvalidOperationException(
                        "Indicá el domicilio de destino o configurá la dirección de recepción del comercio para este operador."),
            command.Fecha,
            command.HoraDesde,
            command.HoraHasta,
            command.Bultos.Select(b =>
                BultoRetiro.Crear(
                    b.Identificacion,
                    b.PesoKg,
                    b.AltoCm,
                    b.AnchoCm,
                    b.ProfundidadCm))
                .ToList(),
            reloj.GetUtcNow());

        // RF37 / RF41: seleccionar exclusivamente
        // la tarifa vigente del servicio de retiro a domicilio.

        var ahora = reloj.GetUtcNow();

        var versionTarifaria = await tarifas.ObtenerVigentePorZonaAsync(
            cuenta.OperadorId,
            zona.Id,
            ahora,
            ct,
            TipoServicioTarifario.RetiroDomicilio);

        if (versionTarifaria is null)
        {
            throw new InvalidOperationException(
                "No existe una tarifa vigente de retiro a domicilio para esta zona.");
        }

        // Reutilizamos la fórmula de peso y volumen.
        // Para el retiro no aplicamos recargo por urgencia.
        var importe = versionTarifaria.Calcular(
            retiro.PesoTotalKg,
            retiro.VolumenTotalMetrosCubicos,
            Modalidad.Estandar);

        retiro.AplicarTarifa(
            versionTarifaria.Id,
            importe);

        await retiros.AgregarAsync(retiro, ct);
        await avisos.PrepararAsync(
            retiro,
            TiposAviso.RetiroSolicitado,
            $"retiro:{retiro.Id}:solicitado",
            retiro.SolicitadoEn,
            ct);

        await unitOfWork.GuardarCambiosAsync(ct);

        return retiro.Id;
    }
}