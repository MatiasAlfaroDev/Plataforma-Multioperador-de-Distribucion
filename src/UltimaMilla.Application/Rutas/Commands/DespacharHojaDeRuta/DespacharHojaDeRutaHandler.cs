
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;

public sealed class DespacharHojaDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
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

        hoja.VerificarEditable();

        var paradas = hoja.Paradas
            .Where(p => p.Activa)
            .OrderBy(p => p.Orden)
            .ToList();

        if (paradas.Count == 0)
        {
            throw new InvalidOperationException(
                "La hoja de ruta no tiene paradas activas para despachar.");
        }

        // Validar toda la hoja antes de modificar estados.
        var enviosPreparados = new List<(
            Envio Envio,
            VersionReglasOperativas Reglas)>();

        foreach (var parada in paradas)
        {
            if (parada.EnvioId is Guid envioId)
            {
                var envio = await envios.ObtenerAsync(
                    envioId, ct)
                    ?? throw new NoEncontradoException(
                        $"No existe el envío de la parada {parada.Id}.");

                if (envio.OperadorId != command.OperadorId)
                {
                    throw new InvalidOperationException(
                        "El envío pertenece a otro operador.");
                }

                if (envio.Estado != EstadoEnvio.AsignadoARuta)
                {
                    throw new InvalidOperationException(
                        $"El envío {envio.ReferenciaExterna} no está asignado a ruta.");
                }

                if (envio.VersionReglasOperativasId is not Guid reglasId)
                {
                    throw new InvalidOperationException(
                        $"El envío {envio.ReferenciaExterna} no tiene reglas operativas.");
                }

                var reglas = await reglasRepository.ObtenerPorIdAsync(
                    command.OperadorId,
                    reglasId,
                    ct)
                    ?? throw new InvalidOperationException(
                        $"No existen las reglas operativas del envío {envio.ReferenciaExterna}.");

                enviosPreparados.Add((envio, reglas));
            }
            else if (parada.RetiroId is Guid retiroId)
            {
                var retiro = await retiros.ObtenerAsync(
                    retiroId, ct)
                    ?? throw new NoEncontradoException(
                        $"No existe el retiro de la parada {parada.Id}.");

                if (retiro.OperadorId != command.OperadorId ||
                    retiro.HojaDeRutaId != hoja.Id ||
                    retiro.Estado != EstadoRetiro.AsignadoARuta)
                {
                    throw new InvalidOperationException(
                        "La parada contiene un retiro que no está correctamente asignado.");
                }

                // No cambiamos el estado del retiro al despachar:
                // permanece AsignadoARuta hasta registrar su resultado.
            }
            else
            {
                throw new InvalidOperationException(
                    "La hoja contiene una parada sin operación válida.");
            }
        }

        var ahora = reloj.GetUtcNow();

        // Mantener las transiciones existentes de entregas.
        foreach (var item in enviosPreparados)
        {
            item.Envio.Aplicar(
                DisparadorEnvio.Despachar,
                item.Reglas,
                ahora,
                "Mobile",
                hoja.RepartidorId.ToString());
        }

        // Permite despachar hojas con entregas, retiros o ambos.
        hoja.MarcarDespachada(ahora);

        await unitOfWork.GuardarCambiosAsync(ct);

        // Se conserva el contrato actual: cantidad de envíos
        // cuyo estado fue actualizado.
        return enviosPreparados.Count;
    }
}
