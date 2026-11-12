using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;

public sealed class ActualizarTarifaHandler(
    IVersionTarifariaRepository tarifas,
    ICuadroTarifarioRepository cuadros,
    IZonaCoberturaRepository zonas,
    IUnitOfWork unitOfWork,
    TimeProvider reloj,
    ICacheService cache)
{
    public async Task<Guid> Handle(
        ActualizarTarifaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.CuadroTarifarioId == Guid.Empty ||
            command.ZonaCoberturaId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador, el cuadro y la zona son obligatorios.");
        }

        if (!Enum.TryParse<TipoServicioTarifario>(
                command.TipoServicio,
                ignoreCase: true,
                out var tipoServicio) ||
            !Enum.IsDefined(tipoServicio))
        {
            throw new ArgumentException(
                "El tipo de servicio debe ser Entrega o RetiroDomicilio.");
        }

        if (command.PrecioBase < 0 ||
            command.PrecioPorKg < 0 ||
            command.PrecioPorMetroCubico < 0 ||
            command.RecargoUrgentePorcentaje < 0 ||
            command.BonificacionPorcentaje is < 0 or > 100)
        {
            throw new ArgumentException(
                "Los precios o porcentajes no son válidos.");
        }

        if (tipoServicio == TipoServicioTarifario.RetiroDomicilio &&
            command.RecargoUrgentePorcentaje != 0)
        {
            throw new ArgumentException(
                "El retiro a domicilio no admite recargo urgente.");
        }

        var ahora = reloj.GetUtcNow();
        Guid nuevaVersionId = Guid.Empty;

        await unitOfWork.EjecutarEnTransaccionAsync(
            async transaccionCt =>
            {
                var zona = await zonas.ObtenerAsync(
                    command.OperadorId,
                    command.ZonaCoberturaId,
                    transaccionCt);

                if (zona is null ||
                    !zona.Activa ||
                    zona.OperadorId != command.OperadorId)
                {
                    throw new InvalidOperationException(
                        "La zona no existe o está inactiva.");
                }

                var cuadro = await cuadros.ObtenerAsync(
                    command.OperadorId,
                    command.CuadroTarifarioId,
                    transaccionCt);

                if (cuadro is null || !cuadro.Activo)
                {
                    throw new InvalidOperationException(
                        "El cuadro tarifario no existe o está inactivo.");
                }

                var versionActual =
                    await tarifas.ObtenerVigentePorZonaAsync(
                        command.OperadorId,
                        command.ZonaCoberturaId,
                        ahora,
                        transaccionCt,
                        tipoServicio);

                if (versionActual is null)
                {
                    throw new InvalidOperationException(
                        "No existe una tarifa vigente para actualizar. " +
                        "Primero debe crearse.");
                }

                if (versionActual.CuadroTarifarioId != cuadro.Id)
                {
                    throw new InvalidOperationException(
                        "El cuadro tarifario no corresponde " +
                        "a la tarifa vigente seleccionada.");
                }

                versionActual.FinalizarVigencia(ahora);

                var nuevaVersion = VersionTarifaria.Crear(
                    command.OperadorId,
                    cuadro.Id,
                    zona.Id,
                    command.PrecioBase,
                    command.PrecioPorKg,
                    command.PrecioPorMetroCubico,
                    command.RecargoUrgentePorcentaje,
                    command.BonificacionPorcentaje,
                    ahora,
                    tipoServicio);

                await tarifas.AgregarAsync(
                    nuevaVersion,
                    transaccionCt);

                await unitOfWork.GuardarCambiosAsync(
                    transaccionCt);

                nuevaVersionId = nuevaVersion.Id;
            },
            ct);

        // La versión nueva pasa a ser la vigente: todas las réplicas lo ven al borrar la entrada compartida.
        await cache.EliminarPorPrefijoAsync(ClavesCache.PrefijoTarifas(command.OperadorId), ct);

        return nuevaVersionId;
    }
}