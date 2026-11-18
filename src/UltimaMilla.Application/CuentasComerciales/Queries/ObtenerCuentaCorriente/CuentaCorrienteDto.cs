namespace UltimaMilla.Application.CuentasComerciales.Queries.ObtenerCuentaCorriente;

/// <summary>
/// Cuenta corriente y liquidaciones del comercio (letra 4, Portal del comercio: "consulta de su cuenta
/// corriente y liquidaciones"). Una cuenta por operador con el que trabaja el comercio.
/// </summary>
public sealed record CuentaCorrienteDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<CuentaCorrienteOperadorDto> Cuentas);

/// <param name="SaldoAnterior">Lo cargado antes del período.</param>
/// <param name="CargosDelPeriodo">Lo cargado dentro del período.</param>
/// <param name="SaldoFinal">SaldoAnterior + CargosDelPeriodo.</param>
/// <param name="ServiciosSinTarifa">Servicios del período que no tienen tarifa calculada: no suman importe.</param>
public sealed record CuentaCorrienteOperadorDto(
    Guid CuentaComercialId,
    Guid OperadorId,
    string Operador,
    decimal SaldoAnterior,
    decimal CargosDelPeriodo,
    decimal SaldoFinal,
    int ServiciosSinTarifa,
    IReadOnlyList<MovimientoCuentaDto> Movimientos,
    IReadOnlyList<LiquidacionMensualDto> Liquidaciones);

/// <param name="Tipo">"Envio" o "Retiro".</param>
/// <param name="Importe">Tarifa calculada al dar de alta el servicio; null si no se pudo calcular.</param>
/// <param name="SaldoAcumulado">Saldo de la cuenta después de este movimiento.</param>
public sealed record MovimientoCuentaDto(
    DateTimeOffset Fecha,
    string Tipo,
    Guid Id,
    string Referencia,
    string Estado,
    decimal? Importe,
    decimal SaldoAcumulado);

/// <summary>Liquidación de un mes calendario: cantidad de servicios e importe total.</summary>
public sealed record LiquidacionMensualDto(
    int Anio,
    int Mes,
    int Envios,
    int Retiros,
    decimal Total);

/// <summary>Un servicio facturable (envío o retiro) de una cuenta comercial, ya reducido a lo que se liquida.</summary>
public sealed record CargoCuenta(
    Guid CuentaComercialId,
    DateTimeOffset Fecha,
    string Tipo,
    Guid Id,
    string Referencia,
    string Estado,
    decimal? Importe);
