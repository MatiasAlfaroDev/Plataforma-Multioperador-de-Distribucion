using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.Envios.Queries.ObtenerDetalleEnvio;

public sealed class ObtenerDetalleEnvioHandler(
    IEnvioRepository envios,
    ICuentaComercialRepository cuentas,
    IVersionReglasOperativasRepository reglasRepository,
    ITenantContext tenantContext)
{
    public async Task<DetalleEnvioDto> Handle(
    ObtenerDetalleEnvioQuery query,
    CancellationToken ct)
{
    if (query.EnvioId == Guid.Empty)
        throw new ArgumentException("El envío es obligatorio.");

    if (!tenantContext.EstaAutenticado ||
        tenantContext.UsuarioId is null)
    {
        throw new UnauthorizedAccessException(
            "Debe iniciar sesión para consultar un envío.");
    }

    var esOperador = Perfiles.PersonalOperador.Contains(tenantContext.Perfil);

    var esComercio = tenantContext.Perfil is
        Perfiles.AdministradorComercio or Perfiles.Comercio;

    if (!esOperador && !esComercio)
    {
        throw new UnauthorizedAccessException(
            "No tiene permisos para consultar envíos.");
    }

    UltimaMilla.Domain.Envios.Envio? envio;

if (esComercio)
{
    var comercioId = tenantContext.ComercioId
        ?? throw new UnauthorizedAccessException(
            "No se pudo identificar el comercio autenticado.");

    envio = await envios.ObtenerPorComercioAsync(
        query.EnvioId,
        comercioId,
        ct);
}
else
{
    var operadorId = tenantContext.OperadorId
        ?? throw new UnauthorizedAccessException(
            "No se pudo identificar el operador.");

    envio = await envios.ObtenerAsync(query.EnvioId, ct);

    if (envio is not null && envio.OperadorId != operadorId)
        envio = null;
}

if (envio is null)
{
    throw new NoEncontradoException(
        "El envío no existe.");
}
    var puedeIniciarDevolucion = false;

if (esOperador &&
    envio.VersionReglasOperativasId is Guid versionId &&
    envio.Estado == UltimaMilla.Domain.Envios.EstadoEnvio.EnDeposito &&
    envio.BultosPendientes > 0 &&
    envio.IntentosRealizados > 0)
{
    var reglas = await reglasRepository.ObtenerPorIdAsync(
        envio.OperadorId,
        versionId,
        ct);

    if (reglas is not null)
    {
        puedeIniciarDevolucion =
            envio.PuedeIniciarDevolucion(reglas);
    }
}

    return new DetalleEnvioDto(
        envio.Id,
        envio.ReferenciaExterna,
        envio.OperadorId,
        envio.CuentaComercialId,
        envio.Estado.ToString(),
        envio.Modalidad.ToString(),
        envio.Destinatario.Nombre,
        envio.Direccion.ToString(),
        envio.FechaAlta,
        envio.TarifaCalculada,
        envio.IntentosRealizados,
        envio.BultosEntregados,
        envio.BultosPendientes,
        envio.BultosEnDevolucion,
        envio.BultosDevueltos,
        puedeIniciarDevolucion,

        envio.Bultos
            .Select(b => new DetalleBultoDto(
                b.Id,
                b.Identificacion,
                b.Estado.ToString(),
                b.PesoKg,
                b.AltoCm,
                b.AnchoCm,
                b.ProfundidadCm
            ))
            .ToList(),

        envio.Eventos
            .OrderByDescending(e => e.OcurridoEn)
            .Select(e => new DetalleEventoDto(
                e.Id,
                e.EstadoAnterior.ToString(),
                e.EstadoNuevo.ToString(),
                e.Disparador.ToString(),
                e.OcurridoEn,
                e.Origen,
                e.Responsable,
                e.Justificacion,
                e.ModalidadDevolucion?.ToString(),
                e.ReceptorDevolucion,
                e.ReferenciaConstanciaDevolucion
            ))
            .ToList()
    );
}
}