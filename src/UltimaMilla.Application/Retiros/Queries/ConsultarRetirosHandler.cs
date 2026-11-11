using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Application.CuentasComerciales;

namespace UltimaMilla.Application.Retiros.Queries;

public sealed class ConsultarRetirosHandler(
    IRetiroRepository retiros,
    ICuentaComercialRepository cuentas,
    ITenantContext tenant,
    IOperadorActivo operadorActivo)
{
    public async Task<IReadOnlyList<RetiroDto>> ListarAsync(
        CancellationToken ct)
    {
        ValidarAutenticacion();

        if (EsOperador())
        {
            var operadorId = ObtenerOperadorSesion();

            var resultado = await retiros.ListarAsync(
                operadorId,
                null,
                ct);

            return resultado
                .Select(Mapear)
                .ToList();
        }

        if (EsComercio())
        {
            var vinculaciones = await ObtenerCuentasComercioAsync(ct);
            var resultado = new List<RetiroDto>();

            foreach (var cuenta in vinculaciones)
            {
                operadorActivo.SeleccionarOperador(cuenta.OperadorId);

                var retirosCuenta = await retiros.ListarAsync(
                    cuenta.OperadorId,
                    cuenta.Id,
                    ct);

                resultado.AddRange(retirosCuenta.Select(Mapear));
            }

            return resultado
                .DistinctBy(r => r.Id)
                .OrderByDescending(r => r.SolicitadoEn)
                .ToList();
        }

        throw new UnauthorizedAccessException(
            "El perfil no tiene acceso a los retiros.");
    }

    public async Task<RetiroDto?> ObtenerAsync(
        Guid retiroId,
        CancellationToken ct)
    {
        ValidarAutenticacion();

        if (retiroId == Guid.Empty)
            throw new ArgumentException("El retiro es obligatorio.");

        if (EsOperador())
        {
            var operadorId = ObtenerOperadorSesion();

            var retiro = await retiros.ObtenerAsync(
                retiroId,
                ct);

            return retiro is not null &&
                retiro.OperadorId == operadorId
                ? Mapear(retiro)
                : null;
        }

        if (EsComercio())
        {
            var vinculaciones = await ObtenerCuentasComercioAsync(ct);

            foreach (var cuenta in vinculaciones)
            {
                operadorActivo.SeleccionarOperador(cuenta.OperadorId);

                var retiro = await retiros.ObtenerAsync(
                    retiroId,
                    ct);

                if (retiro is not null &&
                    retiro.OperadorId == cuenta.OperadorId &&
                    retiro.CuentaComercialId == cuenta.Id)
                {
                    return Mapear(retiro);
                }
            }

            return null;
        }

        throw new UnauthorizedAccessException(
            "El perfil no tiene acceso a los retiros.");
    }

    private async Task<IReadOnlyList<CuentaComercialDto>>
        ObtenerCuentasComercioAsync(CancellationToken ct)
    {
        if (tenant.ComercioId is not Guid comercioId)
        {
            throw new UnauthorizedAccessException(
                "No se identificó el comercio autenticado.");
        }

        var vinculaciones = await cuentas.ListarPorComercioAsync(
            comercioId,
            ct);

        return vinculaciones
            .Where(c =>
                c.ComercioId == comercioId &&
                c.Activa &&
                c.Estado == EstadoCuentaComercial.Confirmada)
            .ToList();
    }

    private Guid ObtenerOperadorSesion()
    {
        if (tenant.OperadorId is not Guid operadorId ||
            operadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "No se identificó el operador autenticado.");
        }

        return operadorId;
    }

    private void ValidarAutenticacion()
    {
        if (!tenant.EstaAutenticado ||
            !tenant.UsuarioId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión para consultar retiros.");
        }
    }

    private bool EsOperador() =>
        tenant.Perfil is
            Perfiles.AdministradorOperador or
            Perfiles.Operador;

    private bool EsComercio() =>
        tenant.Perfil is
            Perfiles.AdministradorComercio or
            Perfiles.Comercio;

    private static RetiroDto Mapear(Retiro retiro)
    {
        return new RetiroDto(
            retiro.Id,
            retiro.OperadorId,
            retiro.CuentaComercialId,
            retiro.Motivo,
            new DireccionRetiroDto(
                retiro.DomicilioRetiro.Calle,
                retiro.DomicilioRetiro.Numero,
                retiro.DomicilioRetiro.Localidad,
                retiro.DomicilioRetiro.Departamento),
            new DireccionRetiroDto(
                retiro.DomicilioDestino.Calle,
                retiro.DomicilioDestino.Numero,
                retiro.DomicilioDestino.Localidad,
                retiro.DomicilioDestino.Departamento),
            retiro.FechaSolicitada,
            retiro.HoraDesde,
            retiro.HoraHasta,
            retiro.Estado.ToString(),
            retiro.SolicitadoEn,
            retiro.HojaDeRutaId,
            retiro.RetiradoEn,
            retiro.ReferenciaFotografia,
            retiro.ReferenciaFirma,
            retiro.PesoTotalKg,
            retiro.VolumenTotalMetrosCubicos,
            retiro.VersionTarifariaId,
            retiro.TarifaCalculada,
            retiro.Bultos
                .Select(b => new BultoRetiroDto(
                    b.Id,
                    b.Identificacion,
                    b.PesoKg,
                    b.AltoCm,
                    b.AnchoCm,
                    b.ProfundidadCm,
                    b.VolumenMetrosCubicos))
                .ToList());
    }
}