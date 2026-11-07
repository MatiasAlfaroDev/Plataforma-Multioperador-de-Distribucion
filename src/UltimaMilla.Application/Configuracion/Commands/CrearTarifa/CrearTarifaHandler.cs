using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Commands.CrearTarifa;

public sealed class CrearTarifaHandler(
    ICuadroTarifarioRepository cuadros,
    IVersionTarifariaRepository tarifas,
    IZonaCoberturaRepository zonas,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<Guid> Handle(
        CrearTarifaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.ZonaCoberturaId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador y la zona son obligatorios.");
        }

        if (string.IsNullOrWhiteSpace(command.Nombre) ||
            command.Nombre.Trim().Length > 150)
        {
            throw new ArgumentException(
                "El nombre debe tener entre 1 y 150 caracteres.");
        }

        if (!Enum.TryParse<TipoServicioTarifario>(
                command.TipoServicio,
                true,
                out var tipoServicio) ||
            !Enum.IsDefined(tipoServicio))
        {
            throw new ArgumentException(
                "El servicio debe ser Entrega o RetiroDomicilio.");
        }

        if (command.PrecioBase < 0 ||
            command.PrecioPorKg < 0 ||
            command.PrecioPorMetroCubico < 0 ||
            command.RecargoUrgentePorcentaje < 0 ||
            command.BonificacionPorcentaje < 0 ||
            command.BonificacionPorcentaje > 100)
        {
            throw new ArgumentException(
                "Los importes o porcentajes no son válidos.");
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

                var vigente = await tarifas.ObtenerVigentePorZonaAsync(
                    command.OperadorId,
                    command.ZonaCoberturaId,
                    ahora,
                    transaccionCt,
                    tipoServicio);

                if (vigente is not null)
                {
                    throw new InvalidOperationException(
                        "Ya existe una tarifa vigente para esta zona " +
                        "y servicio. Debe editarla para crear otra versión.");
                }

                var nombre = command.Nombre.Trim();

                if (await cuadros.ExisteNombreAsync(
                        command.OperadorId,
                        nombre,
                        transaccionCt))
                {
                    throw new InvalidOperationException(
                        "Ya existe un cuadro tarifario con ese nombre.");
                }

                var cuadro = CuadroTarifario.Crear(
                    command.OperadorId,
                    nombre);

                var version = VersionTarifaria.Crear(
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

                await cuadros.AgregarAsync(cuadro, transaccionCt);
                await tarifas.AgregarAsync(version, transaccionCt);

                await unitOfWork.GuardarCambiosAsync(transaccionCt);

                nuevaVersionId = version.Id;
            },
            ct);

        return nuevaVersionId;
    }
}