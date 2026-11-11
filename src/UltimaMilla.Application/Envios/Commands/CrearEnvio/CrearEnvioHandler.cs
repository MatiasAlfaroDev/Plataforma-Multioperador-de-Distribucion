using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Domain.Organizacion;

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
    IOperadorActivo operadorActivo,
    TimeProvider reloj,
    ICacheService cache)
{
    public async Task<CrearEnvioResultado> Handle(
    CrearEnvioCommand command,
    CancellationToken ct)
{
    if (!tenantContext.EstaAutenticado ||
    !tenantContext.UsuarioId.HasValue)
{
    throw new UnauthorizedAccessException(
        "Debe iniciar sesión para crear envíos.");
}

await validator.ValidateAndThrowAsync(command, ct);

if (tenantContext.Perfil is not (
    Perfiles.AdministradorOperador or
    Perfiles.Operador or
    Perfiles.AdministradorComercio or
    Perfiles.Comercio))
{
    throw new UnauthorizedAccessException(
        "Su perfil no tiene permisos para crear envíos.");
}

CuentaComercial cuenta;

if (tenantContext.ComercioId.HasValue)
{
    // Primero identificamos la cuenta solicitada dentro de las
    // vinculaciones del comercio autenticado.
    var vinculaciones = await cuentas.ListarPorComercioAsync(
        tenantContext.ComercioId.Value, ct);

    var vinculacion = vinculaciones.FirstOrDefault(
        c => c.Id == command.CuentaComercialId);

    if (vinculacion is null ||
        !vinculacion.Activa ||
        vinculacion.Estado != EstadoCuentaComercial.Confirmada)
    {
        throw new UnauthorizedAccessException(
            "No tiene una vinculación activa para esta cuenta comercial.");
    }

    // La vinculación ya fue validada: establecemos el operador
    // activo para esta solicitud HTTP.
    operadorActivo.SeleccionarOperador(vinculacion.OperadorId);

    // Ahora el filtro global de EF Core permite obtener la cuenta.
    cuenta = await cuentas.ObtenerAsync(command.CuentaComercialId, ct)
        ?? throw new NoEncontradoException(
            "La cuenta comercial no existe.");

    if (cuenta.ComercioId != tenantContext.ComercioId.Value)
    {
        throw new UnauthorizedAccessException(
            "La cuenta comercial no pertenece a este comercio.");
    }
}
else
{
    if (!tenantContext.OperadorId.HasValue)
    {
        throw new UnauthorizedAccessException(
            "No tiene un operador asignado.");
    }

    cuenta = await cuentas.ObtenerAsync(command.CuentaComercialId, ct)
        ?? throw new NoEncontradoException(
            "La cuenta comercial no existe.");

    if (cuenta.OperadorId != tenantContext.OperadorId.Value)
    {
        throw new UnauthorizedAccessException(
            "La cuenta comercial no pertenece a su operador.");
    }
}

if (!cuenta.Activa ||
    cuenta.Estado != EstadoCuentaComercial.Confirmada)
{
    throw new UnauthorizedAccessException(
        "La cuenta comercial no está confirmada y activa.");
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

        // Invalidar el listado general del operador.
        var claveOperador = $"envios:operador:{envio.OperadorId}";

        await cache.EliminarAsync(claveOperador, ct);

        // Invalidar el listado del comercio correspondiente.
        var claveComercio =
            $"envios:operador:{envio.OperadorId}:cuenta:{envio.CuentaComercialId}";

        await cache.EliminarAsync(claveComercio, ct);

        return new CrearEnvioResultado(
        envio.Id,
        envio.TokenSeguimientoOriginal
            ?? throw new InvalidOperationException(
                "No se generó el token de seguimiento."));
        }
}
