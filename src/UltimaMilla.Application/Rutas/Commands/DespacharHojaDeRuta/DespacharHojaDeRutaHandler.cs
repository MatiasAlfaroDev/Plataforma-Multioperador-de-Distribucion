
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;

public sealed class DespacharHojaDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<int> Handle(
        DespacharHojaDeRutaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.HojaDeRutaId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador y la hoja de ruta son obligatorios.");
        }

        var hoja = await hojas.ObtenerAsync(
            command.OperadorId,
            command.HojaDeRutaId,
            ct)
            ?? throw new NoEncontradoException(
                "La hoja de ruta no existe.");

        var paradas = hoja.Paradas
            .Where(p => p.Activa)
            .OrderBy(p => p.Orden)
            .ToList();

        if (paradas.Count == 0)
        {
            throw new InvalidOperationException(
                "La hoja de ruta no tiene envíos activos para despachar.");
        }

        // Primero validamos y obtenemos las reglas de todos los envíos.
        // Todavía no modificamos ningún estado.
        var enviosPreparados = new List<(
            Envio Envio,
            VersionReglasOperativas Reglas)>();

        foreach (var parada in paradas)
        {
            var envio = await envios.ObtenerAsync(
                parada.EnvioId,
                ct)
                ?? throw new NoEncontradoException(
                    $"No existe el envío asociado a la parada {parada.Id}.");

            if (envio.OperadorId != command.OperadorId)
            {
                throw new InvalidOperationException(
                    "El envío pertenece a otro operador.");
            }

            if (envio.Estado != EstadoEnvio.AsignadoARuta)
            {
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} no está " +
                    "en estado AsignadoARuta.");
            }

            if (envio.VersionReglasOperativasId is not Guid reglasId)
            {
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} " +
                    "no tiene reglas operativas asignadas.");
            }

            var reglas = await reglasRepository.ObtenerPorIdAsync(
                command.OperadorId,
                reglasId,
                ct)
                ?? throw new InvalidOperationException(
                    $"No se encontraron las reglas operativas " +
                    $"del envío {envio.ReferenciaExterna}.");

            enviosPreparados.Add((envio, reglas));
        }

        // Todas las validaciones finalizaron correctamente.
        var ahora = reloj.GetUtcNow();

        foreach (var item in enviosPreparados)
        {
            item.Envio.Aplicar(
                DisparadorEnvio.Despachar,
                item.Reglas,
                ahora,
                "Mobile",
                hoja.RepartidorId.ToString());
        }
        hoja.MarcarDespachada(ahora);

        // Una sola operación de persistencia para todos los envíos.
        await unitOfWork.GuardarCambiosAsync(ct);

        return enviosPreparados.Count;
    }
}
