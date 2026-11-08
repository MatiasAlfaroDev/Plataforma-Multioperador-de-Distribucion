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

    var esOperador = tenantContext.Perfil is
        Perfiles.AdministradorOperador or Perfiles.Operador;

    var esComercio = tenantContext.Perfil is
        Perfiles.AdministradorComercio or Perfiles.Comercio;

    if (!esOperador && !esComercio)
    {
        throw new UnauthorizedAccessException(
            "No tiene permisos para consultar envíos.");
    }

    var envio = await envios.ObtenerAsync(query.EnvioId, ct)
        ?? throw new NoEncontradoException("El envío no existe.");

    if (esOperador)
    {
        if (tenantContext.OperadorId is not Guid operadorId ||
            envio.OperadorId != operadorId)
        {
            throw new NoEncontradoException("El envío no existe.");
        }
    }

    if (esComercio)
    {
        if (tenantContext.ComercioId is not Guid comercioId)
        {
            throw new UnauthorizedAccessException(
                "No se pudo identificar el comercio autenticado.");
        }

        var cuenta = await cuentas.ObtenerPorOperadorYComercioAsync(
            envio.OperadorId,
            comercioId,
            ct);

        if (cuenta is null ||
            cuenta.Id != envio.CuentaComercialId)
        {
            throw new NoEncontradoException("El envío no existe.");
        }
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