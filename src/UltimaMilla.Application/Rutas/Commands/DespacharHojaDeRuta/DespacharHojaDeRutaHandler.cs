
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

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
            switch (parada.TipoOperacion)
            {
                case TipoOperacionRuta.Entrega:
                case TipoOperacionRuta.DevolucionComercio:
                {
                    var envioId = parada.EnvioId
                        ?? throw new InvalidOperationException(
                            "La parada no tiene un envío asociado.");

                    var envio = await envios.ObtenerAsync(envioId, ct)
                        ?? throw new NoEncontradoException(
                            $"No existe el envío de la parada {parada.Id}.");

                    if (envio.OperadorId != command.OperadorId)
                        throw new InvalidOperationException(
                            "El envío pertenece a otro operador.");

                    if (parada.TipoOperacion ==
                        TipoOperacionRuta.DevolucionComercio)
                    {
                        if (envio.Estado != EstadoEnvio.EnDevolucion ||
                            !envio.Bultos.Any(b =>
                                b.Estado == EstadoBulto.EnDevolucion))
                        {
                            throw new InvalidOperationException(
                                "La devolución no está disponible para despachar.");
                        }

                        // Permanece EnDevolucion.
                        break;
                    }

                    if (envio.Estado != EstadoEnvio.AsignadoARuta)
                        throw new InvalidOperationException(
                            $"El envío {envio.ReferenciaExterna} no está asignado a ruta.");

                    if (envio.VersionReglasOperativasId is not Guid reglasId)
                        throw new InvalidOperationException(
                            $"El envío {envio.ReferenciaExterna} no tiene reglas operativas.");

                    var reglas = await reglasRepository.ObtenerPorIdAsync(
                        command.OperadorId,
                        reglasId,
                        ct)
                        ?? throw new InvalidOperationException(
                            $"No existen las reglas operativas del envío {envio.ReferenciaExterna}.");

                    enviosPreparados.Add((envio, reglas));
                    break;
                }

                case TipoOperacionRuta.Retiro:
                case TipoOperacionRuta.EntregaRetiroComercio:
                {
                    var retiroId = parada.RetiroId
                        ?? throw new InvalidOperationException(
                            "La parada no tiene un retiro asociado.");

                    var retiro = await retiros.ObtenerAsync(retiroId, ct)
                        ?? throw new NoEncontradoException(
                            $"No existe el retiro de la parada {parada.Id}.");

                    if (retiro.OperadorId != command.OperadorId)
                        throw new InvalidOperationException(
                            "El retiro pertenece a otro operador.");

                    if (parada.TipoOperacion == TipoOperacionRuta.Retiro)
                    {
                        if (retiro.HojaDeRutaId != hoja.Id ||
                            retiro.Estado != EstadoRetiro.AsignadoARuta)
                        {
                            throw new InvalidOperationException(
                                "El retiro no está correctamente asignado.");
                        }
                    }
                    else
                    {
                        if (retiro.Estado != EstadoRetiro.EnDeposito ||
                            retiro.Bultos.Count == 0)
                        {
                            throw new InvalidOperationException(
                                "El retiro no está disponible para entregar al comercio.");
                        }

                        // Permanece EnDeposito.
                    }

                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "La hoja contiene una operación inválida.");
            }
        }

        var ahora = reloj.GetUtcNow();

        // RF 17: el despachador despacha desde el Backoffice; los envíos salen a nombre del repartidor de la hoja.
        // Después el repartidor verifica la carga del vehículo en la app (RF 19).
        foreach (var item in enviosPreparados)
        {
            item.Envio.Aplicar(
                DisparadorEnvio.Despachar,
                item.Reglas,
                ahora,
                "Backoffice",
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
