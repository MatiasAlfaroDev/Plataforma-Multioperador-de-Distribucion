using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.Envios.Commands.CrearEnvio;

/// <summary>
/// Caso de uso "dar de alta un envío" (RF 7): valida, busca la cuenta comercial,
/// controla duplicados, crea el Envio con las reglas del dominio y confirma con el Unit of Work.
/// Publica el evento EnvioCreado (RNF 6.8).
/// </summary>
public sealed class CrearEnvioHandler(
    IEnvioRepository envios,
    ICuentaComercialRepository cuentas,
    IVersionTarifariaRepository tarifas,
    IVersionReglasOperativasRepository reglas,
    IUnitOfWork unitOfWork,
    IPublicadorDeEventos eventos,
    IValidator<CrearEnvioCommand> validator,
    ITenantContext tenantContext,
    TimeProvider reloj)
{
    public async Task<Guid> Handle(
    CrearEnvioCommand command,
    CancellationToken ct)
{
    if (!tenantContext.EstaAutenticado ||
        !tenantContext.OperadorId.HasValue ||
        !tenantContext.UsuarioId.HasValue)
    {
        throw new UnauthorizedAccessException(
            "Debe iniciar sesión y seleccionar un operador.");
    }

    await validator.ValidateAndThrowAsync(command, ct);

    var cuenta = await cuentas.ObtenerAsync(
        command.CuentaComercialId, ct)
        ?? throw new NoEncontradoException(
            "La cuenta comercial no existe.");

    if (cuenta.OperadorId != tenantContext.OperadorId.Value)
    {
        throw new UnauthorizedAccessException(
            "La cuenta comercial no pertenece al operador seleccionado.");
    }

    if (tenantContext.Perfil is not (
    Perfiles.AdministradorOperador or
    Perfiles.Operador or
    Perfiles.Comercio))
{
    throw new UnauthorizedAccessException(
        "Su perfil no tiene permisos para crear envíos.");
}

if (tenantContext.Perfil == Perfiles.Comercio &&
    tenantContext.ComercioId != cuenta.Id)
{
    throw new UnauthorizedAccessException(
        "No tiene permisos para crear envíos con esta cuenta comercial.");
}

    // El resto del método continúa igual.

        var referencia = command.ReferenciaExterna.Trim();
        if (await envios.ExisteReferenciaAsync(cuenta.Id, referencia, ct))
            throw new ConflictoException($"Ya existe un envío con la referencia '{referencia}' en esta cuenta.");
        var ahora = reloj.GetUtcNow();

        var envio = Envio.Crear(
            cuenta,
            referencia,
            Enum.Parse<Modalidad>(command.Modalidad, ignoreCase: true),
            new Destinatario(command.DestinatarioNombre, command.DestinatarioTelefono),
            new Direccion(command.Calle, command.Numero, command.Localidad, command.Departamento),
            command.Bultos.Select(b => new NuevoBulto(b.PesoKg, b.AltoCm, b.AnchoCm, b.ProfundidadCm)).ToList(),
            ahora);

        var versionTarifaria = await tarifas.ObtenerVigenteAsync(
            cuenta.OperadorId,
            command.Departamento,
            ahora,
            ct)
            ?? throw new NoEncontradoException(
                $"No existe una tarifa vigente para el departamento '{command.Departamento}'.");

        var tarifaCalculada = versionTarifaria.Calcular(
            envio.PesoTotalKg,
            envio.VolumenTotalMetrosCubicos,
            envio.Modalidad);

        envio.AplicarTarifa(
            versionTarifaria.Id,
            tarifaCalculada);

        var versionReglas = await reglas.ObtenerVigenteAsync(
            cuenta.OperadorId,
            ahora,
            ct)
            ?? throw new NoEncontradoException(
                "No existe una versión de reglas operativas vigente para este operador.");

        envio.AsignarReglasOperativas(versionReglas.Id);
        await envios.AgregarAsync(envio, ct);
        
        // va antes de guardar porque se confirma en la misma transacción que el envío (outbox, RNF 6.8)
        await eventos.PublicarAsync(new EnvioCreado(envio.Id, envio.OperadorId, envio.CuentaComercialId, envio.ReferenciaExterna, envio.FechaAlta), ct);

        await unitOfWork.GuardarCambiosAsync(ct);
        return envio.Id;
    }
}
