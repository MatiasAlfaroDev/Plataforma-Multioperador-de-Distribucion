using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Application.CuentasComerciales.Queries.ObtenerCuentaCorriente;

/// <summary>
/// Cuenta corriente del comercio autenticado (Portal). Cada envÃ­o y cada retiro carga en la cuenta
/// su tarifa calculada al momento del alta (RF 9), que no cambia aunque despuÃ©s cambie el cuadro
/// tarifario (6.6). Los retiros cancelados no se cobran. El comercio solo ve sus propias cuentas
/// (segundo nivel de aislamiento, 6.5).
/// </summary>
public sealed class ObtenerCuentaCorrienteHandler(
    ICuentaComercialRepository cuentas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    ITenantContext tenant,
    IOperadorActivo operadorActivo)
{
    public async Task<CuentaCorrienteDto> Handle(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        if (!tenant.EstaAutenticado)
            throw new UnauthorizedAccessException("Debe iniciar sesiÃ³n.");

        if (tenant.Perfil is not (Perfiles.AdministradorComercio or Perfiles.Comercio))
            throw new UnauthorizedAccessException("La cuenta corriente es del comercio.");

        if (tenant.ComercioId is not Guid comercioId)
            throw new UnauthorizedAccessException("No se identificÃ³ el comercio autenticado.");

        if (hasta < desde || hasta == DateOnly.MaxValue)
            throw new ArgumentException("Rango de fechas no vÃ¡lido.");

        // Las cuentas confirmadas, aunque estÃ©n inactivas: lo que ya se cargÃ³ sigue en la cuenta.
        var vinculaciones = (await cuentas.ListarPorComercioAsync(comercioId, ct))
            .Where(c => c.ComercioId == comercioId && c.Estado == EstadoCuentaComercial.Confirmada)
            .ToList();

        var cargos = new List<CargoCuenta>();

        if (vinculaciones.Count > 0)
        {
            var listaEnvios = await envios.ListarPorCuentasComercialesAsync(
                vinculaciones.Select(c => c.Id).ToArray(), ct);

            cargos.AddRange(listaEnvios.Select(e => new CargoCuenta(
                e.CuentaComercialId,
                e.FechaAlta,
                "Envio",
                e.Id,
                e.ReferenciaExterna,
                e.Estado.ToString(),
                e.TarifaCalculada)));
        }

        foreach (var cuenta in vinculaciones)
        {
            // Mismo esquema que ConsultarRetirosHandler: el filtro global de inquilino toma el operador activo.
            operadorActivo.SeleccionarOperador(cuenta.OperadorId);

            var retirosCuenta = await retiros.ListarAsync(cuenta.OperadorId, cuenta.Id, ct);

            cargos.AddRange(retirosCuenta
                .Where(r => r.CuentaComercialId == cuenta.Id && r.Estado.ToString() != "Cancelado")
                .Select(r => new CargoCuenta(
                    r.CuentaComercialId,
                    r.SolicitadoEn,
                    "Retiro",
                    r.Id,
                    r.Motivo,
                    r.Estado.ToString(),
                    r.TarifaCalculada)));
        }

        return CalculadoraCuentaCorriente.Calcular(vinculaciones, cargos, desde, hasta);
    }
}

/// <summary>CÃ¡lculo puro de la cuenta corriente, separado del acceso a datos para poder probarlo.</summary>
public static class CalculadoraCuentaCorriente
{
    public static CuentaCorrienteDto Calcular(
        IReadOnlyList<CuentaComercialDto> cuentas,
        IReadOnlyList<CargoCuenta> cargos,
        DateOnly desde,
        DateOnly hasta)
    {
        var inicio = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var fin = new DateTimeOffset(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var resultado = new List<CuentaCorrienteOperadorDto>();

        foreach (var cuenta in cuentas.OrderBy(c => c.OperadorNombre))
        {
            // DistinctBy: un mismo servicio no se cobra dos veces aunque llegue repetido.
            var deLaCuenta = cargos
                .Where(c => c.CuentaComercialId == cuenta.Id)
                .DistinctBy(c => (c.Tipo, c.Id))
                .OrderBy(c => c.Fecha)
                .ThenBy(c => c.Referencia)
                .ToList();

            var saldoAnterior = deLaCuenta
                .Where(c => c.Fecha < inicio)
                .Sum(c => c.Importe ?? 0m);

            var delPeriodo = deLaCuenta
                .Where(c => c.Fecha >= inicio && c.Fecha < fin)
                .ToList();

            var saldo = saldoAnterior;
            var movimientos = new List<MovimientoCuentaDto>(delPeriodo.Count);

            foreach (var cargo in delPeriodo)
            {
                saldo += cargo.Importe ?? 0m;
                movimientos.Add(new MovimientoCuentaDto(
                    cargo.Fecha, cargo.Tipo, cargo.Id, cargo.Referencia, cargo.Estado, cargo.Importe, saldo));
            }

            var liquidaciones = delPeriodo
                .GroupBy(c => (c.Fecha.UtcDateTime.Year, c.Fecha.UtcDateTime.Month))
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new LiquidacionMensualDto(
                    g.Key.Year,
                    g.Key.Month,
                    g.Count(c => c.Tipo == "Envio"),
                    g.Count(c => c.Tipo == "Retiro"),
                    g.Sum(c => c.Importe ?? 0m)))
                .ToList();

            var cargosDelPeriodo = delPeriodo.Sum(c => c.Importe ?? 0m);

            resultado.Add(new CuentaCorrienteOperadorDto(
                cuenta.Id,
                cuenta.OperadorId,
                cuenta.OperadorNombre,
                saldoAnterior,
                cargosDelPeriodo,
                saldoAnterior + cargosDelPeriodo,
                delPeriodo.Count(c => !c.Importe.HasValue),
                movimientos,
                liquidaciones));
        }

        return new CuentaCorrienteDto(desde, hasta, resultado);
    }
}

